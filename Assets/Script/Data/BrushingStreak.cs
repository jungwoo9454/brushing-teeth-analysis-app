using System;
using System.Collections.Generic;

/// <summary>
/// 양치 기록 목록에서 '연속 양치일수'를 계산하는 순수 함수(상태 없음 → static).
/// 가장 최근 기록일을 기준으로 하루씩 거슬러 올라가며 기록이 끊기지 않은 날의 수를 센다.
/// 같은 날 여러 번 양치해도 1일로 계산한다.
/// (BrushingScorer와 같은 결, 저장소·UI에 의존하지 않는 계산 유틸.)
/// </summary>
public static class BrushingStreak
{
    /// <param name="records">양치 기록(순서 무관). recordedAtUnix를 로컬 날짜로 환산해 사용.</param>
    /// <returns>가장 최근 기록일을 마지막으로 하는 연속 양치일수. 기록이 없으면 0.</returns>
    public static int Compute(IList<BrushingRecord> records)
    {
        if (records == null || records.Count == 0) return 0;

        // 기록들의 '로컬 날짜'(시각 제거) 집합 — 같은 날 중복 제거.
        var days = new HashSet<DateTime>();
        foreach (var r in records)
        {
            if (r == null) continue;
            var localDate = DateTimeOffset.FromUnixTimeSeconds(r.recordedAtUnix).ToLocalTime().Date;
            days.Add(localDate);
        }
        if (days.Count == 0) return 0;

        // 가장 최근 날짜를 찾는다.
        DateTime latest = DateTime.MinValue;
        foreach (var d in days)
            if (d > latest) latest = d;

        // latest부터 하루씩 내려가며 연속으로 기록이 있는 동안 카운트.
        int streak = 0;
        for (var cursor = latest; days.Contains(cursor); cursor = cursor.AddDays(-1))
            streak++;

        return streak;
    }
}
