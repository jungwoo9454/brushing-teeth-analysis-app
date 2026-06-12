using System;

using UnityEngine;

/// <summary>
/// YOLOv8n-pose v3로 칫솔을 검출하는 컴포넌트.
/// 입력 텍스처(WebCamTexture / RenderTexture / Texture2D)를 받아
/// 가장 conf가 높은 detection 1개를 BrushDetection 구조체로 반환한다.
/// FP16/FP32 ModelAsset을 둘 다 받아 인스펙터 토글로 런타임 스위치.
/// 학습 시 keypoint swap(라벨 오류 보정)을 적용 — head = kp[1], handle = kp[0].
/// </summary>
public class ToothbrushDetector : MonoBehaviour, IDisposable
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Model Assets — 둘 다 연결하면 토글로 스위치")]
    [Tooltip("FP32 YOLO ONNX. 호환성 우선 시 사용.")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset yoloFp32;
    [Tooltip("FP16 YOLO ONNX. 모바일에서 1.5~2배 빠름.")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset yoloFp16;

    [Header("Runtime Options")]
    [Tooltip("FP16 사용 여부. 둘 중 하나만 연결됐으면 자동 폴백.")]
    [SerializeField] private bool useFp16 = true;
    [Tooltip("Sentis backend. YOLO는 GPUCompute 권장.")]
    [SerializeField] private Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.GPUCompute;

    // ─────────────────────────────────────────────────────────────
    // 런타임 자원
    // ─────────────────────────────────────────────────────────────

    // Sentis Worker — Awake에서 초기화, OnDestroy(Dispose)에서 해제.
    private Unity.InferenceEngine.Worker worker;

    // YOLO 입력용 640×640 RenderTexture. letterbox 결과를 담는다.
    private RenderTexture inputRt;

    // Sentis input Tensor — 매 프레임 재할당하지 않고 in-place로 채워 재사용 (GC 회피).
    private Unity.InferenceEngine.Tensor<float> inputTensor;

    // 마지막 추론에서 사용된 letterbox 변환 파라미터 — 좌표 역변환에 필수.
    // 원본_좌표 = (yolo_좌표 - pad) / scale.
    private float lastScale;
    private float lastPadX;
    private float lastPadY;

    /// <summary>
    /// YOLOv8-pose 1-class output 채널 수.
    /// 4(bbox cx, cy, w, h) + 1(conf) + 2 × 3(2 keypoints의 x, y, visibility) = 11.
    /// </summary>
    private const int OUTPUT_CHANNELS = 11;

    /// <summary>
    /// letterbox 패딩색. ultralytics YOLO가 학습·추론 전처리에서 쓰는 (114,114,114) 회색.
    /// 검정/흰색 패딩은 입력 분포를 어긋나게 해 검출 정확도를 떨어뜨린다(unity_port §3).
    /// </summary>
    private static readonly Color GRAY_PAD = new Color(114f / 255f, 114f / 255f, 114f / 255f, 1f);

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake() => InitWorker();
    private void OnDestroy() => Dispose();

    /// <summary>Worker / Tensor / RT를 일괄 해제. OnDestroy에서 호출.</summary>
    public void Dispose()
    {
        worker?.Dispose(); worker = null;
        inputTensor?.Dispose(); inputTensor = null;
        if (inputRt != null) { inputRt.Release(); Destroy(inputRt); inputRt = null; }
    }

    /// <summary>토글에 따라 ModelAsset 선택, Worker와 캐시 자원 초기화.</summary>
    private void InitWorker()
    {
        // 토글된 쪽이 비어 있으면 다른 쪽으로 자동 폴백 — 둘 다 비면 에러 로그.
        var asset = useFp16 ? yoloFp16 : yoloFp32;
        if (asset == null) asset = useFp16 ? yoloFp32 : yoloFp16;
        if (asset == null)
        {
            Debug.LogError("[ToothbrushDetector] YOLO ModelAsset이 연결되지 않았다 (FP32/FP16 둘 다 null).");
            return;
        }

        // ONNX → Sentis Model → Worker.
        var model = Unity.InferenceEngine.ModelLoader.Load(asset);
        worker = new Unity.InferenceEngine.Worker(model, backend);

        // 입력 Tensor와 RT를 미리 만들어두고 매 프레임 재사용.
        int s = MlConstants.YOLO_INPUT_SIZE;
        inputTensor = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 3, s, s));
        inputRt = new RenderTexture(s, s, 0, RenderTextureFormat.ARGB32) { useMipMap = false };
        inputRt.Create();
    }

    // ─────────────────────────────────────────────────────────────
    // 외부에서 호출하는 추론 진입점
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 한 프레임에 대해 칫솔 검출 추론을 수행한다.
    /// </summary>
    /// <param name="source">원본 카메라 텍스처. WebCamTexture / RenderTexture / Texture2D 모두 허용.</param>
    /// <returns>BrushDetection (hasDetection=false면 미검출 또는 conf 0.01 미달).</returns>
    public BrushDetection Detect(Texture source)
    {
        var result = new BrushDetection { hasDetection = false };
        if (worker == null || source == null) return result;

        // 1) 원본 → 640×640 letterbox (비율 보존). lastScale/padX/padY 갱신.
        DrawLetterbox(source);

        // 2) RT → Tensor 변환. 기존 inputTensor에 in-place로 채워 매 프레임 할당 회피.
        Unity.InferenceEngine.TextureConverter.ToTensor(inputRt, inputTensor, default);

        // 3) 추론 스케줄. Sentis는 비동기 큐잉이지만 다음 ReadbackAndClone에서 동기화.
        worker.Schedule(inputTensor);

        // 4) 출력 텐서 다운로드.
        //    PeekOutput은 Worker가 소유한 view를 반환하므로 ReadbackAndClone으로 CPU 사본 생성.
        var outRaw = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        if (outRaw == null) return result;
        using var outCpu = outRaw.ReadbackAndClone();
        var data = outCpu.DownloadToArray();

        // 5) shape 가정: [1, 11, anchors]. anchors는 보통 8400(imgsz=640 기준).
        //    layout: data[c * anchors + i] = output[0, c, i].
        int anchors = data.Length / OUTPUT_CHANNELS;
        if (anchors <= 0) return result;

        // 6) confidence channel(=4)에서 argmax. conf 0.01 미달은 미검출로 간주.
        int bestIdx = -1;
        float bestConf = MlConstants.YOLO_CONF_THRESHOLD;
        int confBase = 4 * anchors;
        for (int i = 0; i < anchors; i++)
        {
            float c = data[confBase + i];
            if (c > bestConf) { bestConf = c; bestIdx = i; }
        }
        if (bestIdx < 0) return result;

        // 7) bbox: (cx, cy, w, h) — letterbox 좌표계 (0~640).
        float cx = data[0 * anchors + bestIdx];
        float cy = data[1 * anchors + bestIdx];
        float bw = data[2 * anchors + bestIdx];
        float bh = data[3 * anchors + bestIdx];

        // 8) letterbox → 원본 좌표계로 역변환. bbox는 (xMin, yMin, w, h) Rect 형식으로 저장.
        float xMin  = (cx - bw * 0.5f - lastPadX) / lastScale;
        float yMin  = (cy - bh * 0.5f - lastPadY) / lastScale;
        float wOrig = bw / lastScale;
        float hOrig = bh / lastScale;
        result.bbox = new Rect(xMin, yMin, wOrig, hOrig);

        // 9) Keypoint 추출 — layout:
        //      kp0.x = data[5*anchors + i], kp0.y = data[6*anchors + i], kp0.vis = data[7*anchors + i]
        //      kp1.x = data[8*anchors + i], kp1.y = data[9*anchors + i], kp1.vis = data[10*anchors + i]
        //    학습 라벨 swap 보정: head = kp[1], handle = kp[0]. (HANDOFF / 학습 코드 참고)
        Vector2 handleRaw = new Vector2(data[5 * anchors + bestIdx], data[6 * anchors + bestIdx]);
        Vector2 headRaw   = new Vector2(data[8 * anchors + bestIdx], data[9 * anchors + bestIdx]);

        // YOLO 미검출 keypoint는 (0, 0)으로 출력됨 — 학습 코드의 (kx>0 or ky>0) 체크 그대로.
        result.hasHandle = handleRaw.x > 0f || handleRaw.y > 0f;
        result.hasHead   = headRaw.x   > 0f || headRaw.y   > 0f;
        result.handle    = UnletterboxPoint(handleRaw);
        result.head      = UnletterboxPoint(headRaw);

        result.conf = bestConf;
        result.hasDetection = true;
        return result;
    }

    // ─────────────────────────────────────────────────────────────
    // letterbox 처리 (비율 보존 리사이즈)
    // ─────────────────────────────────────────────────────────────

    /// <summary>letterbox 좌표의 한 점을 원본 이미지 좌표로 역변환.</summary>
    private Vector2 UnletterboxPoint(Vector2 p) =>
        new Vector2((p.x - lastPadX) / lastScale, (p.y - lastPadY) / lastScale);

    /// <summary>
    /// source를 inputRt(640×640)에 letterbox로 그린다 — ultralytics YOLO 전처리와 동일.
    /// 비율을 보존해 가장 긴 변을 640에 맞추고, 남는 영역은 회색(114)으로 패딩한다.
    /// stretch(비율 왜곡)나 검정 패딩은 입력 분포를 어긋나게 해 검출 정확도를 크게
    /// 떨어뜨린다 — 특히 칫솔이 작거나 부분 가려지는 어금니 자세에서 검출이 실패한다.
    ///
    /// Graphics.Blit은 dest 전체를 덮어써 letterbox 영역 제한이 불가능하므로
    /// (GL.Viewport를 무시), GL.LoadPixelMatrix + Graphics.DrawTexture로
    /// 지정 사각형에만 source를 그린다. 참조: unity_port/INTEGRATION.md §3.
    /// </summary>
    private void DrawLetterbox(Texture source)
    {
        int s = MlConstants.YOLO_INPUT_SIZE;
        float srcW = source.width;
        float srcH = source.height;
        if (srcW <= 0f || srcH <= 0f) return;

        // 비율 보존 스케일 — 긴 변을 s에 맞춤. 짧은 변에 padding 발생.
        lastScale = Mathf.Min(s / srcW, s / srcH);
        float scaledW = srcW * lastScale;
        float scaledH = srcH * lastScale;
        lastPadX = (s - scaledW) * 0.5f;
        lastPadY = (s - scaledH) * 0.5f;

        var prev = RenderTexture.active;
        RenderTexture.active = inputRt;

        // 남는 영역을 ultralytics와 동일한 회색(114/255)으로 채운다.
        GL.Clear(true, true, GRAY_PAD);

        // 픽셀 좌표계(원점 좌상단, y 아래로 +)에서 letterbox 사각형에만 source를 그린다.
        // Graphics.DrawTexture는 Graphics.Blit과 달리 지정 Rect 영역에만 그린다.
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, s, s, 0f);
        Graphics.DrawTexture(new Rect(lastPadX, lastPadY, scaledW, scaledH), source);
        GL.PopMatrix();

        RenderTexture.active = prev;
    }
}
