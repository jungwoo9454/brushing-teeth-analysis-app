using UnityEngine;
using UnityEngine.UI;  // RawImage — 카메라 미리보기 표시용

/// <summary>
/// 양치 ML 파이프라인의 조립자(MonoBehaviour).
/// 매 프레임 MediaPipe → MouthInfo → YOLO → FeatureBuilder → MLP → Engagement → Stability →
/// UI 매핑 순서로 실행하고, 결과를 EventBus.Publish(ZoneDetectedEvent)로 발행한다.
/// 양치 씬의 메인 GameObject에 부착하여 사용.
///
/// 인스펙터 연결:
///   - ToothbrushDetector / ZoneMlpClassifier 컴포넌트(같은 GameObject 권장)
///   - faceProviderBehaviour: IFaceLandmarksProvider를 구현한 사용자 MediaPipe Runner
/// </summary>
[RequireComponent(typeof(ToothbrushDetector))]
[RequireComponent(typeof(ZoneMlpClassifier))]
public class BrushingPipeline : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Camera Source")]
    [Tooltip("빈 문자열이면 첫 번째 카메라 자동 선택. 'FaceTime HD Camera' 등 디바이스명으로 지정 가능.")]
    [SerializeField] private string webcamDeviceName = "";
    [Tooltip("요청 해상도 — 디바이스가 지원하지 않으면 가장 가까운 해상도로 폴백.")]
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;
    [SerializeField] private int requestedFps = 30;
    [Tooltip("전면(셀카) 카메라는 보통 좌우 반전되어 들어옴 — 학습 분포와 맞추려면 unmirror 필수.")]
    [SerializeField] private bool unmirrorFrontCamera = true;

    [Header("ML Components — 같은 GameObject에 부착 권장")]
    [SerializeField] private ToothbrushDetector detector;
    [SerializeField] private ZoneMlpClassifier classifier;

    [Header("Face Landmarks Provider")]
    [Tooltip("IFaceLandmarksProvider 인터페이스를 구현한 MonoBehaviour를 드래그.")]
    [SerializeField] private MonoBehaviour faceProviderBehaviour;

    [Header("Preview UI (선택)")]
    [Tooltip("카메라 미리보기를 표시할 RawImage. 연결하면 WebCamTexture를 그대로 그려준다 (셀카 모드에선 좌우 반전된 친숙한 화면).")]
    [SerializeField] private RawImage previewImage;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // faceProviderBehaviour를 IFaceLandmarksProvider로 캐스팅한 결과 — Awake에서 한 번 수행.
    private IFaceLandmarksProvider faceProvider;

    // 카메라 입력 텍스처(원본).
    private WebCamTexture webcam;

    // unmirror 결과를 담을 RenderTexture. unmirror가 필요 없을 땐 null로 유지.
    private RenderTexture sourceRt;

    // 시간 안정화 — 매 프레임 raw zone을 Push.
    private readonly StabilityBuffer stability = new StabilityBuffer();

    // 14-D feature 출력 버퍼 — 매 프레임 재사용해 GC 회피.
    private readonly float[] featBuffer = new float[FeatureBuilder.FEATURE_DIM];

    // YAW guard를 radian으로 미리 변환 — 매 프레임 Deg2Rad 계산 회피.
    private static readonly float YAW_GUARD_RAD = MlConstants.YAW_GUARD_DEG * Mathf.Deg2Rad;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Editor/Player가 background로 가도 Update가 멈추지 않도록 — 카메라 권한 prompt나 다른 창 focus 시
        // Game View가 멈춰서 Update가 안 도는 케이스를 방지한다.
        Application.runInBackground = true;
        Debug.Log("[BrushingPipeline] Awake 진입");

        // SerializeField로 안 주입된 경우 같은 GameObject에서 자동 탐색 — RequireComponent 덕에 반드시 존재.
        if (detector == null)   detector   = GetComponent<ToothbrushDetector>();
        if (classifier == null) classifier = GetComponent<ZoneMlpClassifier>();

        // Provider 인터페이스 캐스팅. 미구현이면 에러 로그 후 Update에서 스킵.
        faceProvider = faceProviderBehaviour as IFaceLandmarksProvider;
        if (faceProvider == null)
            Debug.LogError("[BrushingPipeline] faceProviderBehaviour가 IFaceLandmarksProvider를 구현하지 않았다.");
        else
            Debug.Log("[BrushingPipeline] faceProvider OK: " + faceProvider.GetType().Name);
    }

    private void Start()
    {
        Debug.Log($"[BrushingPipeline] Start — WebCamTexture.devices.Length = {WebCamTexture.devices.Length}");
        StartCamera();
        Debug.Log($"[BrushingPipeline] StartCamera 완료 — webcam null? {webcam == null}, deviceName={webcam?.deviceName}, isPlaying={webcam?.isPlaying}");

        // 미리보기 RawImage가 연결돼 있으면 WebCamTexture를 직접 바인딩.
        // WebCamTexture는 Unity가 매 프레임 갱신하므로 한 번만 할당해두면 RawImage가 자동으로 최신 프레임을 그린다.
        if (previewImage != null && webcam != null)
        {
            previewImage.texture = webcam;
            // AspectRatioFitter 비율 갱신은 Start 시점이 아니라 첫 카메라 프레임을 받은 직후로 미룬다 —
            // Start에서는 webcam.width/height가 아직 placeholder(16x16)라서 잘못된 비율로 잡힘.
            Debug.Log("[BrushingPipeline] previewImage에 WebCamTexture 바인딩 완료.");
        }

        // FaceProvider가 MediaPipeFaceProvider인 경우, BrushingPipeline의 webcam을 입력 텍스처로 주입.
        // (DummyFaceProvider 등 다른 구현체에는 영향 없음 — 그쪽은 외부 입력이 필요 없는 stub.)
        if (faceProvider is MediaPipeFaceProvider mpProvider && webcam != null)
        {
            mpProvider.SetInputTexture(webcam);
            Debug.Log("[BrushingPipeline] MediaPipeFaceProvider에 webcam 입력 주입 완료.");
        }
    }

    private void OnDestroy()
    {
        if (webcam != null) webcam.Stop();
        if (sourceRt != null) { sourceRt.Release(); Destroy(sourceRt); sourceRt = null; }
    }

    // 진단용: Update 첫 진입과 카메라 첫 프레임 수신 시점을 한 번씩만 로그.
    private bool _diagFirstTickLogged;
    private bool _diagFirstFrameLogged;

    // 진단용: 1초 throttle로 face/brush 상태를 콘솔에 찍기 위한 타이머.
    // pitch 부호 검증·detection 비율 확인 등에 사용. 검증 끝나면 이 블록 제거 가능.
    private float _diagNextLogTime;

    private void Update()
    {
        // Update 자체가 도는지 확인용 — Awake/Start 로그는 떴는데 이 로그가 안 뜨면 Update 자체가 안 도는 상황.
        if (!_diagFirstTickLogged)
        {
            _diagFirstTickLogged = true;
            Debug.Log($"[BrushingPipeline] First Update tick — webcam null? {webcam == null}, webcam.isPlaying={webcam?.isPlaying}, didUpdate={webcam?.didUpdateThisFrame}");
        }

        // 카메라/Provider 준비 안 됐으면 이번 프레임 스킵 — 단, 빈 이벤트는 발행해서 UI가 None을 인지하게 한다.
        if (webcam == null || !webcam.didUpdateThisFrame) return;

        // 카메라 첫 프레임 수신 시점 진단 — 이게 안 뜨면 카메라가 실제로 안 돌고 있는 것.
        if (!_diagFirstFrameLogged)
        {
            _diagFirstFrameLogged = true;
            Debug.Log($"[BrushingPipeline] First camera frame received — {webcam.width}x{webcam.height}");

            // 이제 실제 카메라 해상도가 확정됐으므로 RawImage의 RectTransform을 직접 계산해 fit-in-parent로 배치.
            // AspectRatioFitter는 driven property 충돌이 있어서 stretch anchors 환경에서 의도대로 동작하지 않는다.
            // 직접 계산해 부모(Canvas) 영역 안에 카메라 비율로 가장 크게 들어가는 크기로 sizeDelta를 잡는다.
            FitPreviewToParent(webcam.width, webcam.height);
        }

        if (faceProvider == null) { PublishEmpty(); return; }

        // ── 단계 1: 카메라 원본 → (필요 시) unmirror된 텍스처. ──
        Texture source = PrepareSource();

        // ── 단계 2: MediaPipe Runner에서 최신 landmark + transform 가져오기. ──
        if (!faceProvider.TryGetLatest(out var landmarks, out var faceTransform))
        {
            PublishEmpty();
            return;
        }

        // ── 단계 3: MouthInfo 빌드 (각도 정렬 polygon, 4점 평균 center, polygon bbox dim, pitch). ──
        var face = MediaPipeFacePort.Build(landmarks, source.width, source.height, faceTransform);

        // ── 단계 4: 옆모습/입 비율 극단치 가드. 통과 못 하면 raw zone을 강제 None으로. ──
        bool guardOk = IsFaceUsable(face);

        // ── 단계 5: YOLO 칫솔 검출 (conf 0.01, swap 적용). ──
        var brush = detector.Detect(source);

        // 진단: 1초마다 face/brush 상태 한 줄 — pitch 부호/검출 비율 검증용.
        // 라디안을 degree로 변환해 직관적으로 (정면≈0, 숙임/들기에 따라 부호 확인).
        if (Time.time >= _diagNextLogTime)
        {
            _diagNextLogTime = Time.time + 1f;
            Debug.Log(
                $"[Diag] face={face.isValid}, pitchDeg={face.pitch * Mathf.Rad2Deg:F1}, " +
                $"yawDeg={face.yaw * Mathf.Rad2Deg:F1}, mw={face.width:F0}, mh={face.height:F0}, " +
                $"brush.det={brush.hasDetection}, brush.conf={brush.conf:F3}, " +
                $"head=({brush.head.x:F0},{brush.head.y:F0})");
        }

        // ── 단계 6: feature 빌드 → MLP 추론. 가드 실패면 MLP 호출 자체를 스킵해서 비용 절감. ──
        MlZone rawZone;
        if (!guardOk)
        {
            rawZone = MlZone.None;
        }
        else
        {
            FeatureBuilder.Build(face, brush, featBuffer);
            rawZone = classifier.Predict(featBuffer);
        }

        // ── 단계 7: Engagement 체크. 미통과 시 zone을 None으로 강제. ──
        bool engaged = false;
        if (rawZone != MlZone.None && brush.hasDetection && brush.hasHead && face.isValid)
        {
            engaged = EngagementChecker.IsEngaged(
                brush.head, brush.bbox, face.polygon,
                MlConstants.MOUTH_OVERLAP_THRESHOLD, out _);
        }
        if (!engaged) rawZone = MlZone.None;

        // ── 단계 8: stability buffer로 시간 안정화 (window=3, min_count=2). ──
        MlZone stable = stability.Push(rawZone);

        // ── 단계 9: 5-zone UI 매핑 + 이벤트 발행. ──
        EventBus.Publish(new ZoneDetectedEvent
        {
            rawZone    = rawZone,
            stableZone = stable,
            uiZones    = UIZoneMapper.Map(stable),
            isEngaged  = engaged,
            hasFace    = face.isValid,
            hasBrush   = brush.hasDetection,
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 보조 메서드
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 카메라 원본을 그대로 또는 unmirror된 RenderTexture로 반환.
    /// 전면(셀카) 카메라일 때 학습 분포와 일치시키기 위해 좌우 반전을 풀어준다.
    /// </summary>
    private Texture PrepareSource()
    {
        // 토글 OFF이거나 후면 카메라면 그대로 — 불필요한 Blit 회피.
        bool needUnmirror = unmirrorFrontCamera && IsCurrentDeviceFrontFacing();
        if (!needUnmirror) return webcam;

        // RT 준비 (해상도 변경 시 재생성).
        if (sourceRt == null || sourceRt.width != webcam.width || sourceRt.height != webcam.height)
        {
            if (sourceRt != null) { sourceRt.Release(); Destroy(sourceRt); }
            sourceRt = new RenderTexture(webcam.width, webcam.height, 0, RenderTextureFormat.ARGB32);
            sourceRt.Create();
        }

        // Graphics.Blit으로 horizontal flip — scale.x=-1, offset.x=1 이면 x 좌표가 (1-x)로 매핑된다.
        Graphics.Blit(webcam, sourceRt, new Vector2(-1f, 1f), new Vector2(1f, 0f));
        return sourceRt;
    }

    /// <summary>현재 사용 중인 카메라가 전면 카메라인지 WebCamDevice 정보로 조회.</summary>
    private bool IsCurrentDeviceFrontFacing()
    {
        if (webcam == null) return false;
        var devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++)
        {
            // 디바이스 이름 일치하는 항목의 isFrontFacing 플래그 반환.
            if (devices[i].name == webcam.deviceName) return devices[i].isFrontFacing;
        }
        // 일치 디바이스를 못 찾으면 후면 가정 — 불필요한 flip을 회피하는 보수적 선택.
        return false;
    }

    /// <summary>
    /// 추가 가드: face 검출 성공 + 정면 자세(|yaw| ≤ 45°) + 입 비율 정상 범위(0.1~5).
    /// 학습 데이터 분포 밖 입력은 None 처리 — MLP 외삽 위험 회피.
    /// </summary>
    private static bool IsFaceUsable(in MouthInfo face)
    {
        if (!face.isValid) return false;
        if (Mathf.Abs(face.yaw) > YAW_GUARD_RAD) return false;
        // face.height > MIN_MOUTH_HEIGHT는 MediaPipeFacePort.Build 안에서 이미 보장됨.
        float aspect = face.width / face.height;
        if (aspect < MlConstants.MOUTH_ASPECT_MIN || aspect > MlConstants.MOUTH_ASPECT_MAX) return false;
        return true;
    }

    /// <summary>
    /// 검출 실패/스킵 프레임용 빈 이벤트 발행.
    /// stableZone은 StabilityBuffer 내부 상태(직전 값)를 그대로 사용해 UI 카운터 플리커를 방지.
    /// </summary>
    private void PublishEmpty()
    {
        EventBus.Publish(new ZoneDetectedEvent
        {
            rawZone    = MlZone.None,
            stableZone = stability.Current,
            uiZones    = UIZoneMapper.Map(stability.Current),
            isEngaged  = false,
            hasFace    = false,
            hasBrush   = false,
        });
    }

    /// <summary>
    /// previewImage의 RectTransform을 카메라 비율에 맞춰 부모(Canvas) 영역 안에 fit-in-parent로 배치.
    /// AspectRatioFitter를 안 쓰는 이유: stretch anchors와의 driven property 충돌로 의도와 다르게 동작.
    /// </summary>
    private void FitPreviewToParent(int camW, int camH)
    {
        if (previewImage == null || camW <= 0 || camH <= 0) return;

        var rt = previewImage.rectTransform;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        // 부모 Canvas 영역의 실제 크기 (CanvasScaler 적용 후 logical 크기).
        float parentW = parent.rect.width;
        float parentH = parent.rect.height;
        if (parentW <= 0 || parentH <= 0) return;

        // 카메라/부모 비율 비교로 가로/세로 어느 쪽을 기준으로 fit할지 결정.
        float camAspect = (float)camW / camH;
        float parentAspect = parentW / parentH;
        float w, h;
        if (parentAspect > camAspect)
        {
            // 부모가 더 가로로 김 → 높이 기준으로 fit, 좌우에 빈 영역.
            h = parentH;
            w = h * camAspect;
        }
        else
        {
            // 부모가 더 세로로 김 → 너비 기준으로 fit, 위아래에 빈 영역.
            w = parentW;
            h = w / camAspect;
        }

        // anchors를 center로 고정해 sizeDelta가 실제 픽셀 크기를 의미하게 한다.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);

        Debug.Log($"[BrushingPipeline] FitPreviewToParent — cam={camW}x{camH}, parent={parentW:F0}x{parentH:F0}, fit={w:F0}x{h:F0}");
    }

    /// <summary>WebCamTexture 생성·Play. 디바이스가 없으면 에러 로그.</summary>
    private void StartCamera()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("[BrushingPipeline] 카메라 장치를 찾을 수 없다.");
            return;
        }

        // 디바이스명이 비어있으면 첫 번째 카메라 자동 선택, 지정됐으면 해당 디바이스 강제 사용.
        webcam = string.IsNullOrEmpty(webcamDeviceName)
            ? new WebCamTexture(requestedWidth, requestedHeight, requestedFps)
            : new WebCamTexture(webcamDeviceName, requestedWidth, requestedHeight, requestedFps);
        webcam.Play();
    }
}
