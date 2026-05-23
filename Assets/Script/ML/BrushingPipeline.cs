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
    // 외부 공개 상태 — 추후 기능(AR 피드백, 미니게임 등)이 매 프레임 폴링해 쓸 수 있다.
    // EventBus의 ZoneDetectedEvent를 구독하지 않고도 현재 양치 구역을 바로 읽는 용도.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 가장 최근에 확정된 양치 구역(8-class, engagement·stability 적용 후의 stableZone).
    /// 양치 중이 아니거나 미검출 상태가 이어지면 MlZone.None.
    /// </summary>
    public MlZone CurrentZone { get; private set; } = MlZone.None;

    /// <summary>
    /// CurrentZone을 UI 5-zone으로 매핑한 결과. SideLeft/SideRight는 2개로 분기되며,
    /// None이면 빈 배열. UIZoneMapper가 캐시한 배열이므로 호출자는 읽기 전용으로만 사용할 것.
    /// </summary>
    public UiZone[] CurrentUiZones { get; private set; } = System.Array.Empty<UiZone>();

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

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Editor/Player가 background로 가도 Update가 멈추지 않도록 — 카메라 권한 prompt 등으로 focus를
        // 잃는 순간 Game View가 멈춰서 ML 파이프라인이 정지하는 케이스를 방지한다.
        Application.runInBackground = true;

        // SerializeField로 안 주입된 경우 같은 GameObject에서 자동 탐색 — RequireComponent 덕에 반드시 존재.
        if (detector == null)   detector   = GetComponent<ToothbrushDetector>();
        if (classifier == null) classifier = GetComponent<ZoneMlpClassifier>();

        // Provider 인터페이스 캐스팅. 미구현이면 에러 로그 후 Update에서 스킵.
        faceProvider = faceProviderBehaviour as IFaceLandmarksProvider;
        if (faceProvider == null)
            Debug.LogError("[BrushingPipeline] faceProviderBehaviour가 IFaceLandmarksProvider를 구현하지 않았다.");
    }

    private void Start()
    {
        StartCamera();

        // 미리보기 RawImage가 연결돼 있으면 WebCamTexture를 직접 바인딩.
        // WebCamTexture는 Unity가 매 프레임 갱신하므로 한 번만 할당해두면 RawImage가 자동으로 최신 프레임을 그린다.
        // RectTransform 비율 fit은 카메라 첫 프레임을 받은 직후 Update에서 한 번만 수행(현재 webcam.width/height는 placeholder).
        if (previewImage != null && webcam != null)
            previewImage.texture = webcam;

        // FaceProvider가 MediaPipeFaceProvider인 경우, BrushingPipeline의 webcam을 입력 텍스처로 주입.
        // (DummyFaceProvider 등 다른 구현체에는 영향 없음 — 그쪽은 외부 입력이 필요 없는 stub.)
        // YOLO는 PrepareSource()에서 전면 카메라를 unmirror하므로, MediaPipe에도 같은
        // unmirror 여부를 알려 두 모델이 동일한 좌표계를 보게 한다.
        if (faceProvider is MediaPipeFaceProvider mpProvider && webcam != null)
        {
            bool needUnmirror = unmirrorFrontCamera && IsCurrentDeviceFrontFacing();
            mpProvider.SetInputTexture(webcam, needUnmirror);
        }
    }

    private void OnDestroy()
    {
        if (webcam != null) webcam.Stop();
        if (sourceRt != null) { sourceRt.Release(); Destroy(sourceRt); sourceRt = null; }
    }

    // 카메라 첫 프레임 수신 시점에 RawImage RectTransform을 카메라 비율로 fit하기 위한 1회성 가드.
    // Start 시점엔 webcam.width/height가 placeholder(16x16)라 정확한 비율을 계산할 수 없다.
    private bool _previewFitted;

    private void Update()
    {
        // 카메라/Provider 준비 안 됐으면 이번 프레임 스킵 — 단, 빈 이벤트는 발행해서 UI가 None을 인지하게 한다.
        if (webcam == null || !webcam.didUpdateThisFrame) return;

        // 카메라 첫 프레임이 들어온 시점에 미리보기 비율을 한 번만 확정.
        if (!_previewFitted)
        {
            _previewFitted = true;
            FitPreviewToParent(webcam.width, webcam.height);
        }

        if (faceProvider == null) { PublishEmpty(); return; }

        // ── 단계 1: 카메라 원본 → (필요 시) unmirror된 텍스처. ──
        Texture source = PrepareSource();

        // ── 단계 2: MediaPipe Runner에서 최신 landmark 가져오기. ──
        // faceTransform은 더 이상 쓰지 않는다 — pitch/yaw는 MediaPipeFacePort가 solvePnP로 구한다.
        if (!faceProvider.TryGetLatest(out var landmarks, out _))
        {
            PublishEmpty();
            return;
        }

        // ── 단계 3: MouthInfo 빌드 (각도 정렬 polygon, 4점 평균 center, polygon bbox dim, pitch). ──
        var face = MediaPipeFacePort.Build(landmarks, source.width, source.height);

        // ── 단계 4: 옆모습/입 비율 극단치 가드. 통과 못 하면 zone을 강제 None으로. ──
        bool guardOk = IsFaceUsable(face);

        // ── 단계 5: YOLO 칫솔 검출 (conf 0.01, swap 적용). ──
        var brush = detector.Detect(source);

        // ── 단계 6: feature 빌드 → MLP 추론. 가드 실패면 MLP 호출 자체를 스킵해서 비용 절감. ──
        MlZone zone;
        if (!guardOk)
        {
            zone = MlZone.None;
        }
        else
        {
            FeatureBuilder.Build(face, brush, featBuffer);
            zone = classifier.Predict(featBuffer);
        }

        // ── 단계 7: Engagement 체크 (head ∈ polygon AND mouth_overlap ≥ 0.10). 미통과 시 zone을 None으로 강제. ──
        bool engaged = false;
        if (brush.hasDetection && brush.hasHead && face.isValid && face.polygon != null)
        {
            engaged = EngagementChecker.IsEngaged(
                brush.head, brush.bbox, face.polygon,
                MlConstants.MOUTH_OVERLAP_THRESHOLD, out _);
        }
        if (!engaged) zone = MlZone.None;

        // ── 단계 8: stability buffer로 시간 안정화 (window=3, min_count=2). ──
        MlZone stable = stability.Push(zone);

        // ── 단계 9: 5-zone UI 매핑 + 외부 공개 상태 갱신 + 이벤트 발행. ──
        var uiZones = UIZoneMapper.Map(stable);
        CurrentZone    = stable;
        CurrentUiZones = uiZones;

        EventBus.Publish(new ZoneDetectedEvent
        {
            rawZone    = zone,
            stableZone = stable,
            uiZones    = uiZones,
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
    /// face 검출 성공 여부만 확인한다.
    /// 파이썬 데모(run_demo.py)·unity_port 파이프라인에는 yaw·입비율 가드가 없다.
    /// 이전의 |yaw|≤45°·aspect 0.1~5 가드는 reference에 없는 Unity 추가물로,
    /// 어금니를 닦으며 고개를 돌리는 정상 자세까지 None 처리해 정합성을 깨뜨렸다 — 제거.
    /// </summary>
    private static bool IsFaceUsable(in MouthInfo face) => face.isValid;

    /// <summary>
    /// 검출 실패/스킵 프레임용 빈 이벤트 발행.
    /// 미검출 프레임도 stability에 None으로 Push한다 — 짧은 끊김은 StabilityBuffer가
    /// 흡수하지만, 충분히 이어지면(idleResetFrames) stableZone이 None으로 떨어진다.
    /// </summary>
    private void PublishEmpty()
    {
        MlZone stable = stability.Push(MlZone.None);
        var uiZones = UIZoneMapper.Map(stable);
        CurrentZone    = stable;
        CurrentUiZones = uiZones;

        EventBus.Publish(new ZoneDetectedEvent
        {
            rawZone    = MlZone.None,
            stableZone = stable,
            uiZones    = uiZones,
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
