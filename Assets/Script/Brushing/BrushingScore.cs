using UnityEngine;

/// <summary>
/// 점수 산출 결과 — 최종 점수(0~100)와 세부 항목(0~1).
/// </summary>
public struct BrushingScoreResult
{
    public int   score;        // 0~100
    public float coverage01;   // 구역 충족도
    public float evenness01;   // 구역 균등도
    public float continuity01; // 지속성(쉬지 않음)
}

/// <summary>
/// 양치 세션 결과를 0~100점으로 환산하는 순수 계산기(상태 없음 → static).
///
/// 평가 축 3가지를 가중 합산한다:
///   1) 구역 충족도(coverage, 60%)
///      5개 구역 각각이 목표시간(perZoneTarget = 3분/5 = 36초)에 얼마나 도달했는지의 평균.
///      안 닦은 구역은 0이 되므로, 골고루 닦아야 점수가 오른다(균등성도 함께 반영).
///   2) 구역 균등도(evenness, 20%)
///      가장 적게 닦은 구역 / 가장 많이 닦은 구역. 한 곳이라도 안 닦으면 0, 완전 균등이면 1.
///   3) 지속성(continuity, 20%)
///      가장 길게 "쉰" 연속 시간이 짧을수록 높다. IdleFullPenaltySeconds(60초) 이상 연속으로
///      쉬면 0 → 3분 동안 쉬지 않고 닦았는지를 본다.
///
/// 경계값:
///   - 3분 내내 모든 구역을 고르게 닦음 → 100점.
///   - 3분 동안 전혀 닦지 않음 → 0점(모든 항목 0).
/// </summary>
public static class BrushingScorer
{
    // 가중치 (합 = 1)
    private const float WeightCoverage   = 0.6f;
    private const float WeightEvenness   = 0.2f;
    private const float WeightContinuity = 0.2f;

    // 이 시간(초) 이상 연속으로 쉬면 지속성 점수 0.
    private const float IdleFullPenaltySeconds = 60f;

    /// <param name="zoneSeconds">구역별 누적 시간(초). 인덱스 = (int)UiZone.</param>
    /// <param name="perZoneTargetSeconds">구역당 목표 시간(초). 보통 sessionSeconds / 5.</param>
    /// <param name="longestIdleSeconds">가장 길게 연속으로 쉰 시간(초).</param>
    public static BrushingScoreResult Compute(
        float[] zoneSeconds, float perZoneTargetSeconds, float longestIdleSeconds)
    {
        var result = new BrushingScoreResult();
        if (zoneSeconds == null || zoneSeconds.Length == 0 || perZoneTargetSeconds <= 0f)
            return result; // 전부 0 → 0점

        int n = zoneSeconds.Length;

        // 1) 구역 충족도 — 각 구역의 목표 도달률(0~1) 평균.
        float coverageSum = 0f;
        float maxZone = 0f;
        float minZone = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float z = Mathf.Max(0f, zoneSeconds[i]);
            coverageSum += Mathf.Clamp01(z / perZoneTargetSeconds);
            if (z > maxZone) maxZone = z;
            if (z < minZone) minZone = z;
        }
        result.coverage01 = coverageSum / n;

        // 2) 구역 균등도 — min/max. 한 곳이라도 0이면 0, 완전 균등이면 1.
        result.evenness01 = maxZone > 0f ? minZone / maxZone : 0f;

        // 3) 지속성 — 가장 길게 쉰 시간이 짧을수록 높음.
        result.continuity01 = Mathf.Clamp01(1f - longestIdleSeconds / IdleFullPenaltySeconds);

        float score01 = WeightCoverage   * result.coverage01
                      + WeightEvenness   * result.evenness01
                      + WeightContinuity * result.continuity01;

        result.score = Mathf.RoundToInt(Mathf.Clamp01(score01) * 100f);
        return result;
    }
}

/// <summary>
/// 양치 점수가 산출되었을 때 발행되는 이벤트(결과/리포트 UI가 구독).
/// BrushingRecordWriter가 저장 직후 발행한다.
/// </summary>
public struct BrushingScoredEvent
{
    public int   score;                 // 0~100
    public float totalBrushingSeconds;  // 실제 양치(engaged) 시간
    public float sessionSeconds;        // 세션 길이(3분 창)
    public float[] zoneSeconds;         // 구역별 누적 시간
    public BrushingScoreResult breakdown;
}
