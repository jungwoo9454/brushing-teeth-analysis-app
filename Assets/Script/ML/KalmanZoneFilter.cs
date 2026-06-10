/// <summary>
/// zone 확률 벡터의 시간 평활 — 칼만 필터(probability EMA).
/// HANDOFF_TO_APP.md §7 반영: 기존 이산 <see cref="StabilityBuffer"/>(window 3 / min 2)를 대체한다.
///
/// 매 프레임 MLP softmax 확률 벡터(8-dim)에 EMA를 적용해 frame-to-frame 튐(jitter)을 줄이고,
/// 평활된 분포의 argmax를 stable zone으로 확정한다.
///   - 양치 검출(engaged) 프레임:   state ← K·probs + (1-K)·state
///   - 미검출/비engaged 프레임:     state ← noneDecay·[None one-hot] + (1-noneDecay)·state
///
/// K=0.3은 핸드오프 실험 최적값(test_set_b strict 41.32% → 43.41%, macro F1 +0.021).
/// 참조: Assets/Model/reference_code/kalman_filter_reference.py.
/// </summary>
public class KalmanZoneFilter
{
    /// <summary>분류 클래스 수(MlZone 8개) — 모델 출력 차원과 1:1.</summary>
    public const int CLASS_COUNT = 8;

    /// <summary>칼만 게인 K — 클수록 최신 프레임 반영↑(평활↓), 작을수록 평활↑. 핸드오프 권장 0.3.</summary>
    public const float DEFAULT_K = 0.3f;

    /// <summary>미검출/비engaged 프레임에서 state를 None 쪽으로 끌어당기는 비율(느린 수렴).</summary>
    public const float DEFAULT_NONE_DECAY = 0.1f;

    private const int NONE_INDEX = (int)MlZone.None;   // 7

    private readonly float k;
    private readonly float noneDecay;

    // 평활된 확률 분포(state). 초기 uniform(1/8) — 첫 Push 전까지 어느 쪽으로도 치우치지 않는다.
    private readonly float[] state = new float[CLASS_COUNT];

    /// <summary>기본 파라미터(K=0.3, noneDecay=0.1) 생성자.</summary>
    public KalmanZoneFilter() : this(DEFAULT_K, DEFAULT_NONE_DECAY) { }

    /// <summary>커스텀 파라미터 생성자 — 튜닝/테스트용.</summary>
    public KalmanZoneFilter(float k, float noneDecay)
    {
        this.k = k;
        this.noneDecay = noneDecay;
        Reset();
    }

    /// <summary>현재 평활 분포의 argmax(외부 읽기 전용 — state를 바꾸지 않음).</summary>
    public MlZone Current => (MlZone)ArgMax(state);

    /// <summary>
    /// 양치 검출(engaged) 프레임 — softmax 확률 벡터로 EMA 갱신 후 stable zone 반환.
    /// probs가 null/길이 부족이면 검출 실패로 간주해 None 쪽으로 감쇠한다.
    /// </summary>
    public MlZone PushProbs(float[] probs)
    {
        if (probs == null || probs.Length < CLASS_COUNT) return PushNone();

        for (int i = 0; i < CLASS_COUNT; i++)
            state[i] = k * probs[i] + (1f - k) * state[i];

        return (MlZone)ArgMax(state);
    }

    /// <summary>
    /// 미검출/비engaged 프레임 — state를 None one-hot 쪽으로 느리게 감쇠시킨 뒤 stable zone 반환.
    /// 칫솔을 입에서 떼면 수 프레임 내 None이 우세해진다(무한히 옛 zone을 기억하지 않게).
    /// 분포 정규화는 유지된다: Σstate = (1-noneDecay)·1 + noneDecay·1 = 1.
    /// </summary>
    public MlZone PushNone()
    {
        float keep = 1f - noneDecay;
        for (int i = 0; i < CLASS_COUNT; i++)
            state[i] *= keep;
        state[NONE_INDEX] += noneDecay;   // target = None one-hot → noneDecay·1

        return (MlZone)ArgMax(state);
    }

    /// <summary>state를 uniform(1/8)로 초기화. 양치 세션 시작 등에서 호출.</summary>
    public void Reset()
    {
        float u = 1f / CLASS_COUNT;
        for (int i = 0; i < CLASS_COUNT; i++) state[i] = u;
    }

    /// <summary>벡터 argmax 인덱스(동률은 더 낮은 인덱스). null/빈 배열이면 None 인덱스.</summary>
    public static int ArgMax(float[] v)
    {
        if (v == null || v.Length == 0) return NONE_INDEX;

        int best = 0;
        float bestVal = v[0];
        for (int i = 1; i < v.Length; i++)
            if (v[i] > bestVal) { bestVal = v[i]; best = i; }

        return best;
    }
}
