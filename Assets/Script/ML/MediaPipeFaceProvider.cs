using System;
using UnityEngine;

// homuler/MediaPipeUnity 패키지 — Runner를 쓰지 않고 FaceLandmarker 객체만 직접 사용.
// Mediapipe.Image와 UnityEngine.UI.Image 같은 잠재 충돌을 피하기 위해 typedef-style alias 적용.
using FaceLandmarker = Mediapipe.Tasks.Vision.FaceLandmarker.FaceLandmarker;
using FaceLandmarkerOptions = Mediapipe.Tasks.Vision.FaceLandmarker.FaceLandmarkerOptions;
using FaceLandmarkerResult = Mediapipe.Tasks.Vision.FaceLandmarker.FaceLandmarkerResult;
using MpImage = Mediapipe.Image;
using TasksBaseOptions = Mediapipe.Tasks.Core.BaseOptions;
using TasksRunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;

/// <summary>
/// IFaceLandmarksProvider 구현 — homuler MediaPipe Unity의 FaceLandmarker를 Runner 없이 직접 사용한다.
/// BrushingPipeline이 매 프레임 자신의 WebCamTexture를 SetInputTexture로 주입하면,
/// 내부에서 Image로 변환해 TryDetectForVideo를 호출하고 478 landmark + facial transform matrix를 보관한다.
/// 카메라는 외부(BrushingPipeline)가 소유하므로 Runner의 ImageSource와 충돌할 일이 없다.
/// </summary>
/// <remarks>
/// 사용자 준비 사항:
///   - Google MediaPipe 공식 저장소에서 face_landmarker.task 다운로드
///   - Assets/StreamingAssets/face_landmarker.task 로 배치 (또는 modelAssetPath 절대경로 지정)
///   - 다운로드: https://developers.google.com/mediapipe/solutions/vision/face_landmarker
/// </remarks>
public class MediaPipeFaceProvider : MonoBehaviour, IFaceLandmarksProvider
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Model")]
    [Tooltip("face_landmarker.task 파일 경로. StreamingAssets 안에 두고 'face_landmarker.task'만 적거나, 절대 경로 사용. " +
             "패키지에 모델 내장 안 됨 — 사용자가 직접 다운로드 필요.")]
    [SerializeField] private string modelAssetPath = "face_landmarker.task";

    [Header("Detection Options (학습과 동일하게 유지 권장)")]
    [Tooltip("얼굴 검출 최소 confidence. 학습 설정과 동일.")]
    [SerializeField, Range(0f, 1f)] private float minDetectionConfidence = 0.5f;
    [Tooltip("얼굴 존재 최소 confidence — 트래킹 안정성.")]
    [SerializeField, Range(0f, 1f)] private float minPresenceConfidence = 0.5f;
    [Tooltip("프레임 간 트래킹 최소 confidence.")]
    [SerializeField, Range(0f, 1f)] private float minTrackingConfidence = 0.5f;

    [Header("Input")]
    [Tooltip("MediaPipe에 매 프레임 넘길 입력 텍스처. BrushingPipeline.Start에서 SetInputTexture(webcam)로 주입한다.")]
    [SerializeField] private Texture inputTexture;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // FaceLandmarker 인스턴스 — Awake에서 한 번 생성, OnDestroy에서 dispose.
    private FaceLandmarker _landmarker;

    // 가장 최근의 추론 결과. TryGetLatest가 이걸 읽어서 반환.
    private FaceLandmarkerResult _latestResult;
    private bool _hasResult;

    // 고해상도 단조 증가 timestamp 소스. Environment.TickCount는 Windows에서 ~15ms 해상도라
    // 고FPS 시 연속 프레임이 같은 값을 받아 MediaPipe가 거부한다(monotonically increasing 위반).
    private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();

    // MediaPipe에 마지막으로 넘긴 timestamp(ms) — 엄격한 단조 증가 보장용.
    private long _lastTimestampMs = -1;

    // 진단/사용자 알림용 — 모델 로드 실패 시 매 프레임 에러 도배를 막기 위한 플래그.
    private bool _initFailed;

    // 런타임 추론 예외를 한 번만 로그하기 위한 플래그(_initFailed와 달리 detection을 멈추지 않는다).
    private bool _runtimeErrorLogged;

    // WebCamTexture는 Texture2D를 상속하지 않으므로 매 프레임 픽셀을 복사할 CPU 버퍼.
    // Mediapipe.Image 생성자는 Texture2D만 받아 변환이 강제됨.
    // _cpuTex는 inputTexture 해상도 변경 시에만 재생성 — 매 프레임 GC 없음.
    private Texture2D _cpuTex;
    private Color32[] _pixelBuffer;

    // 좌표계 보정(상하/좌우 반전) 결과를 담는 별도 버퍼 — GetPixels32 원본(_pixelBuffer)을
    // 덮어쓰지 않고 안전하게 변환하기 위해 분리한다.
    private Color32[] _flipBuffer;

    // 입력 텍스처에 가로 반전을 적용할지 여부. 전면(셀카) 카메라를 unmirror해
    // YOLO 입력(unmirror된 source)과 좌표계를 맞추기 위한 플래그. SetInputTexture로 설정.
    private bool _flipHorizontal;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        TryInitLandmarker();
        _clock.Restart();
    }

    private void OnDestroy()
    {
        // FaceLandmarker(BaseVisionTaskApi)는 Close()로 native 리소스 해제 — Dispose는 explicit interface.
        _landmarker?.Close();
        _landmarker = null;

        // CPU 변환 버퍼 정리.
        if (_cpuTex != null) { Destroy(_cpuTex); _cpuTex = null; }
        _pixelBuffer = null;
        _flipBuffer = null;
    }

    private void Update()
    {
        // 초기화 실패했거나 입력이 없으면 이번 프레임 스킵.
        if (_initFailed || _landmarker == null || inputTexture == null) return;

        // 단조 증가 timestamp — TryDetectForVideo는 이전 호출보다 작거나 같으면 에러.
        // Stopwatch는 고해상도(QPC 기반)이고, 그래도 같은 ms가 나오면 직전 값 +1로 강제한다.
        long timestampMs = _clock.ElapsedMilliseconds;
        if (timestampMs <= _lastTimestampMs) timestampMs = _lastTimestampMs + 1;
        _lastTimestampMs = timestampMs;

        try
        {
            // Mediapipe.Image는 Texture2D만 받으므로 WebCamTexture/RT를 CPU 픽셀로 복사해 Texture2D에 채운다.
            var cpuTex = EnsureCpuTexture(inputTexture);
            if (cpuTex == null) return;  // 지원하지 않는 입력 타입 등.

            // using으로 native 메모리 즉시 해제 — 매 프레임 누수 방지.
            using (var image = new MpImage(cpuTex))
            {
                var result = new FaceLandmarkerResult();
                if (_landmarker.TryDetectForVideo(image, timestampMs, null, ref result))
                {
                    _latestResult = result;
                    _hasResult = result.faceLandmarks != null && result.faceLandmarks.Count > 0;
                }
                else
                {
                    _hasResult = false;
                }
            }
        }
        catch (Exception e)
        {
            // 매 프레임 에러 도배 방지 — 한 번만 로그한다. 단, 일시적 예외로 detection을
            // 영구 정지시키지 않는다(_initFailed 재사용 금지) — 다음 프레임에 다시 시도한다.
            if (!_runtimeErrorLogged)
            {
                Debug.LogError("[MediaPipeFaceProvider] 추론 중 예외: " + e.Message);
                _runtimeErrorLogged = true;
            }
            _hasResult = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>외부에서 카메라 텍스처를 주입. BrushingPipeline.Start에서 webcam을 전달한다.</summary>
    public void SetInputTexture(Texture tex) => SetInputTexture(tex, false);

    /// <summary>
    /// 카메라 텍스처 주입 + unmirror 지정.
    /// unmirror=true면 EnsureCpuTexture에서 가로 반전을 적용한다 — 전면(셀카) 카메라의
    /// 좌우 미러를 풀어 YOLO 입력(BrushingPipeline.PrepareSource의 unmirror된 source)과
    /// 동일한 좌표계를 쓰게 한다. 두 모델의 x축이 어긋나면 FeatureBuilder가 (head.x - mcx)를
    /// 잘못 계산해 zone 분류가 틀어진다.
    /// </summary>
    public void SetInputTexture(Texture tex, bool unmirror)
    {
        inputTexture = tex;
        _flipHorizontal = unmirror;
    }

    /// <inheritdoc/>
    public bool TryGetLatest(out Vector3[] normalizedLandmarks, out Matrix4x4? faceTransform)
    {
        if (!_hasResult)
        {
            normalizedLandmarks = null;
            faceTransform = null;
            return false;
        }

        // 478개 normalized landmark 추출 — homuler의 NormalizedLandmark에서 x, y, z를 Vector3로 복사.
        // 매 프레임 새 배열 할당이 GC 부담이지만 정확한 단순성을 우선; 추후 캐시화 여지.
        var landmarks = _latestResult.faceLandmarks[0].landmarks;
        normalizedLandmarks = new Vector3[landmarks.Count];
        for (int i = 0; i < landmarks.Count; i++)
        {
            var l = landmarks[i];
            normalizedLandmarks[i] = new Vector3(l.x, l.y, l.z);
        }

        // facial_transformation_matrixes — outputFaceTransformationMatrixes=true일 때만 채워짐.
        // null/빈 경우 폴백.
        var mats = _latestResult.facialTransformationMatrixes;
        faceTransform = (mats != null && mats.Count > 0) ? mats[0] : (Matrix4x4?)null;
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 입력 Texture(주로 WebCamTexture)를 Mediapipe.Image 호환 Texture2D로 변환해 반환.
    /// 캐시(_cpuTex)를 재사용해 매 프레임 GC 없음 — 해상도 변경 시에만 재할당.
    /// </summary>
    private Texture2D EnsureCpuTexture(Texture src)
    {
        // WebCamTexture는 Color32[] 버퍼로 직접 픽셀 읽기 가능.
        if (src is WebCamTexture webcam)
        {
            int w = webcam.width, h = webcam.height;

            // 해상도 변경 또는 첫 호출 시 버퍼 재생성.
            if (_cpuTex == null || _cpuTex.width != w || _cpuTex.height != h)
            {
                if (_cpuTex != null) Destroy(_cpuTex);
                _cpuTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _pixelBuffer = new Color32[w * h];
                _flipBuffer  = new Color32[w * h];
            }

            // GetPixels32(buffer)는 in-place로 버퍼를 채워 매 프레임 GC를 피한다.
            webcam.GetPixels32(_pixelBuffer);

            // ── 좌표계 보정 ──
            // Mediapipe.Image(Texture2D) 생성자는 Texture2D.GetRawTextureData()를 그대로
            // native ImageFrame으로 넘긴다. 그런데 Unity 텍스처의 raw 데이터는 bottom-up
            // (첫 행 = 이미지 맨 아랫줄)이고 MediaPipe ImageFrame은 top-down을 기대하므로,
            // 보정 없이 넘기면 상하 반전된(거꾸로 선) 얼굴이 들어가 FaceLandmarker가
            // 검출에 거의 실패한다. → 매 프레임 세로 반전 필수.
            // _flipHorizontal이면 가로도 반전해 전면 카메라 unmirror까지 함께 처리한다.
            FlipPixels(_pixelBuffer, _flipBuffer, w, h, _flipHorizontal);
            _cpuTex.SetPixels32(_flipBuffer);
            _cpuTex.Apply(false);
            return _cpuTex;
        }

        // 이미 Texture2D면 변환 불필요 — 그대로 반환.
        if (src is Texture2D tex2D) return tex2D;

        // RenderTexture 같은 다른 타입은 지원 안 함 (필요 시 ReadPixels 경로 추가).
        return null;
    }

    /// <summary>
    /// bottom-up Color32 버퍼(src)를 dst로 복사하며 세로(상하)를 반전한다.
    /// 이렇게 채운 Texture2D를 GetRawTextureData로 읽으면 top-down이 되어
    /// MediaPipe ImageFrame의 좌표계와 일치한다. flipX=true면 가로도 함께 반전.
    /// </summary>
    private static void FlipPixels(Color32[] src, Color32[] dst, int w, int h, bool flipX)
    {
        if (flipX)
        {
            // 상하 + 좌우 동시 반전 — 픽셀 단위 복사.
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w;
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                    dst[dstRow + x] = src[srcRow + (w - 1 - x)];
            }
        }
        else
        {
            // 상하 반전만 — 행 단위 블록 복사로 가볍게 처리.
            for (int y = 0; y < h; y++)
                System.Array.Copy(src, (h - 1 - y) * w, dst, y * w, w);
        }
    }

    /// <summary>
    /// FaceLandmarker.CreateFromOptions로 인스턴스 생성.
    /// 모델 파일이 없거나 옵션이 잘못되면 에러 로그 후 _initFailed=true로 매 프레임 스킵.
    /// </summary>
    private void TryInitLandmarker()
    {
        try
        {
            // 상대 경로면 StreamingAssets 절대 경로로 변환 — MediaPipe는 cwd 기준이라 그대로 두면 NOT_FOUND.
            // 절대 경로(/로 시작)는 그대로 사용.
            var resolvedPath = System.IO.Path.IsPathRooted(modelAssetPath)
                ? modelAssetPath
                : System.IO.Path.Combine(Application.streamingAssetsPath, modelAssetPath);

            var baseOpt = new TasksBaseOptions(modelAssetPath: resolvedPath);
            var options = new FaceLandmarkerOptions(
                baseOptions: baseOpt,
                runningMode: TasksRunningMode.VIDEO,
                numFaces: 1,                      // HANDOFF: max_num_faces=1
                minFaceDetectionConfidence: minDetectionConfidence,
                minFacePresenceConfidence: minPresenceConfidence,
                minTrackingConfidence: minTrackingConfidence,
                outputFaceBlendshapes: false,     // 사용 안 함 — 비활성으로 추론 비용 절감.
                outputFaceTransformationMatrixes: true);  // pitch/yaw 추출에 필수.
            _landmarker = FaceLandmarker.CreateFromOptions(options);
        }
        catch (Exception e)
        {
            _initFailed = true;
            Debug.LogError("[MediaPipeFaceProvider] FaceLandmarker 초기화 실패 — 모델 파일 경로/옵션 확인: " + e);
        }
    }
}
