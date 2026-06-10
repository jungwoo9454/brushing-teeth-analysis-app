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

    // Predict()용 스크래치 버퍼 — PredictProbs와 공유해 매 프레임 할당을 피한다.
    private readonly float[] probsScratch = new float[KalmanZoneFilter.CLASS_COUNT];

    /// <summary>
    /// 14차원 feature → 8-class argmax(MlZone).
    /// (칼만 평활 도입 후 파이프라인은 <see cref="PredictProbs"/>를 쓰지만, 단발 분류·디버그용으로 유지.)
    /// </summary>
    /// <param name="features">길이 FeatureBuilder.FEATURE_DIM(14) 배열. null/길이 mismatch는 None 반환.</param>
    /// <returns>argmax된 MlZone (모델/입력 오류 시 None).</returns>
    public MlZone Predict(float[] features)
    {
        if (!PredictProbs(features, probsScratch)) return MlZone.None;
        return (MlZone)KalmanZoneFilter.ArgMax(probsScratch);
    }

    /// <summary>
    /// 14차원 feature → 8-class <b>softmax 확률 벡터</b>를 outProbs에 채운다(칼만 EMA 입력용).
    /// HANDOFF §7의 평활은 확률 벡터에 적용되므로, argmax가 아닌 정규화된 분포가 필요하다.
    /// </summary>
    /// <param name="features">길이 FeatureBuilder.FEATURE_DIM(14) 배열.</param>
    /// <param name="outProbs">결과를 받을 길이 ≥ 8 배열. 실패 시 None one-hot으로 채운다.</param>
    /// <returns>추론 성공 여부. false면 outProbs는 None one-hot(검출 실패 취급).</returns>
    public bool PredictProbs(float[] features, float[] outProbs)
    {
        if (outProbs == null || outProbs.Length < 8) return false;

        // 사전 가드 — Worker나 입력이 잘못된 경우 None one-hot 폴백.
        if (worker == null || features == null || features.Length != FeatureBuilder.FEATURE_DIM)
        {
            FillNoneOneHot(outProbs);
            return false;
        }

        // 1) 입력 Tensor 생성. shape [1, 14].
        using var input = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, FeatureBuilder.FEATURE_DIM), features);
        worker.Schedule(input);

        // 2) 출력 다운로드 — [1, 8] logits.
        var outRaw = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;
        if (outRaw == null)
        {
            FillNoneOneHot(outProbs);
            return false;
        }
        using var outCpu = outRaw.ReadbackAndClone();
        var logits = outCpu.DownloadToArray();

        // 3) softmax(수치 안정성 위해 max 차감) → outProbs[0..8].
        Softmax(logits, outProbs);
        return true;
    }

    /// <summary>logits → softmax 확률을 outProbs(길이 ≥ 8)에 채운다. logits가 8보다 짧으면 남는 칸은 0.</summary>
    private static void Softmax(float[] logits, float[] outProbs)
    {
        int n = Mathf.Min(logits.Length, 8);
        float max = float.NegativeInfinity;
        for (int i = 0; i < n; i++) if (logits[i] > max) max = logits[i];

        float sum = 0f;
        for (int i = 0; i < n; i++) { float e = Mathf.Exp(logits[i] - max); outProbs[i] = e; sum += e; }
        for (int i = n; i < 8; i++) outProbs[i] = 0f;   // 비정상 export 대비.

        if (sum > 0f)
            for (int i = 0; i < n; i++) outProbs[i] /= sum;
        else
            FillNoneOneHot(outProbs);   // 전부 0인 비정상 출력 — None으로.
    }

    private static void FillNoneOneHot(float[] outProbs)
    {
        for (int i = 0; i < 8; i++) outProbs[i] = 0f;
        outProbs[(int)MlZone.None] = 1f;
    }
}
