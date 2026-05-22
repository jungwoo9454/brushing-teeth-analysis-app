using System;

using UnityEngine;

/// <summary>
/// 14-D feature vector를 입력받아 8-class zone logits를 산출하고 argmax로 MlZone을 반환.
/// 모델은 작은 MLP(14 → 64 → 32 → 8, ~50KB) — HANDOFF 권고대로 CPU 백엔드 사용.
/// (GPU 전송 오버헤드가 모델 계산보다 크므로 CPU가 빠름.)
/// </summary>
public class ZoneMlpClassifier : MonoBehaviour, IDisposable
{
    [Header("Model Asset")]
    [Tooltip("zone_mlp.onnx를 ModelAsset으로 드래그. Assets/Model/models/zone_mlp.onnx를 Sentis가 자동 import.")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset mlpModel;

    [Header("Runtime Options")]
    [Tooltip("MLP는 매우 작아서 CPU가 GPU 전송 오버헤드 대비 빠르다 (HANDOFF 권장).")]
    [SerializeField] private Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU;

    // Sentis Worker — Awake에서 초기화, OnDestroy에서 dispose.
    private Unity.InferenceEngine.Worker worker;

    private void Awake() => InitWorker();
    private void OnDestroy() => Dispose();

    /// <summary>Worker 해제. 외부에서 명시적 정리할 때도 호출 가능.</summary>
    public void Dispose()
    {
        worker?.Dispose();
        worker = null;
    }

    /// <summary>ModelAsset → Model → Worker.</summary>
    private void InitWorker()
    {
        if (mlpModel == null)
        {
            Debug.LogError("[ZoneMlpClassifier] mlpModel(ModelAsset)이 연결되지 않았다.");
            return;
        }
        var model = Unity.InferenceEngine.ModelLoader.Load(mlpModel);
        worker = new Unity.InferenceEngine.Worker(model, backend);
    }

    /// <summary>
    /// 14차원 feature → 8-class argmax(MlZone).
    /// </summary>
    /// <param name="features">길이 FeatureBuilder.FEATURE_DIM(14) 배열. null/길이 mismatch는 None 반환.</param>
    /// <returns>argmax된 MlZone (모델/입력 오류 시 None).</returns>
    public MlZone Predict(float[] features)
    {
        // 사전 가드 — Worker나 입력이 잘못된 경우 즉시 None 폴백.
        if (worker == null) return MlZone.None;
        if (features == null || features.Length != FeatureBuilder.FEATURE_DIM) return MlZone.None;

        // 1) 입력 Tensor 생성. shape [1, 14], 데이터는 features 배열을 복사하여 채움.
        //    MLP는 매우 작아 호출 빈도(~30fps) × 14 float 할당 비용은 무시 가능.
        //    GC 더 줄이려면 inputTensor를 캐시하고 NativeArray 경로로 업로드하는 변형이 있으나
        //    여기선 단순함을 우선.
        using var input = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, FeatureBuilder.FEATURE_DIM), features);
        worker.Schedule(input);

        // 2) 출력 다운로드 — [1, 8] logits.
        var outRaw = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        if (outRaw == null) return MlZone.None;
        using var outCpu = outRaw.ReadbackAndClone();
        var logits = outCpu.DownloadToArray();

        // 3) argmax — softmax는 argmax 결과에 영향 없으므로 생략.
        //    logits 길이는 8이어야 정상이지만, 모델이 다르게 export됐을 가능성 대비해 Min으로 보호.
        int best = 0;
        float bestVal = float.NegativeInfinity;
        int n = Mathf.Min(logits.Length, 8);
        for (int i = 0; i < n; i++)
        {
            if (logits[i] > bestVal) { bestVal = logits[i]; best = i; }
        }

        // 4) int → MlZone 캐스팅. ZONE_LIST 순서(UpperLeft=0 ... None=7)와 일치.
        return (MlZone)best;
    }
}
