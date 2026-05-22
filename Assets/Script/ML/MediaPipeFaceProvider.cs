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

    // 단조 증가 timestamp 보장용 시작 시점 (ms).
    private long _startTickMs;

    // 진단/사용자 알림용 — 모델 로드 실패 시 매 프레임 에러 도배를 막기 위한 플래그.
    private bool _initFailed;

    // WebCamTexture는 Texture2D를 상속하지 않으므로 매 프레임 픽셀을 복사할 CPU 버퍼.
    // Mediapipe.Image 생성자는 Texture2D만 받아 변환이 강제됨.
    // _cpuTex는 inputTexture 해상도 변경 시에만 재생성 — 매 프레임 GC 없음.
    private Texture2D _cpuTex;
    private Color32[] _pixelBuffer;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        TryInitLandmarker();
        _startTickMs = Environment.TickCount;
    }

    private void OnDestroy()
    {
        // FaceLandmarker(BaseVisionTaskApi)는 Close()로 native 리소스 해제 — Dispose는 explicit interface.
        _landmarker?.Close();
        _landmarker = null;

        // CPU 변환 버퍼 정리.
        if (_cpuTex != null) { Destroy(_cpuTex); _cpuTex = null; }
        _pixelBuffer = null;
    }

    private void Update()
    {
        // 초기화 실패했거나 입력이 없으면 이번 프레임 스킵.
        if (_initFailed || _landmarker == null || inputTexture == null) return;

        // 단조 증가 timestamp — TryDetectForVideo는 이전 호출보다 작거나 같으면 에러.
        long timestampMs = Environment.TickCount - _startTickMs;
        if (timestampMs <= 0) timestampMs = 1;

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
            // 매 프레임 에러 도배 방지 — 한 번만 로그.
            if (!_initFailed)
            {
                Debug.LogError("[MediaPipeFaceProvider] 추론 중 예외 — 이후 프레임은 침묵: " + e.Message);
                _initFailed = true;
            }
            _hasResult = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>외부에서 카메라 텍스처를 주입. BrushingPipeline.Start에서 webcam을 전달한다.</summary>
    public void SetInputTexture(Texture tex) => inputTexture = tex;

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
            }

            // GetPixels32(buffer)는 in-place로 버퍼를 채워 매 프레임 GC를 피한다.
            webcam.GetPixels32(_pixelBuffer);
            _cpuTex.SetPixels32(_pixelBuffer);
            _cpuTex.Apply(false);
            return _cpuTex;
        }

        // 이미 Texture2D면 변환 불필요 — 그대로 반환.
        if (src is Texture2D tex2D) return tex2D;

        // RenderTexture 같은 다른 타입은 지원 안 함 (필요 시 ReadPixels 경로 추가).
        return null;
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
