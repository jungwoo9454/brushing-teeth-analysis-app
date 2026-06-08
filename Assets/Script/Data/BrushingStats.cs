using System;
using System.Collections.Generic;

/// <summary>
/// 양치 기록 목록 위에서 통계를 뽑는 순수 함수 모음(상태 없음 → static).
/// 저장소(JSON/DB)·UI에 의존하지 않으며, recordedAtUnix를 로컬 시각으로 환산해 사용한다.
/// (메인 메뉴의 주간 평균·오늘 체크리스트 등에서 사용)
/// </summary>
public static class BrushingStats
{
    /// <summary>그 날짜가 속한 주의 월요일 0시(로컬).</summary>
    public static DateTime WeekStart(DateTime date)
    {
        int offset = ((int)date.DayOfWeek + 6) % 7; // 월=0 ... 일=6
        return date.Date.AddDays(-offset);
    }

    /// <summary>기록의 로컬 시각.</summary>
    public static DateTime LocalTimeOf(BrushingRecord r)
        => DateTimeOffset.FromUnixTimeSeconds(r.recordedAtUnix).ToLocalTime().DateTime;

    /// <summary>[fromInclusive, toExclusive) 구간 기록들의 평균 점수. 건수는 count로 반환(0이면 평균 0).</summary>
    public static float Average(IList<BrushingRecord> records, DateTime fromInclusive, DateTime toExclusive, out int count)
    {
        count = 0;
        double sum = 0;
        if (records != null)
        {
            foreach (var r in records)
            {
                if (r == null) continue;
                var t = LocalTimeOf(r);
                if (t >= fromInclusive && t < toExclusive)
                {
                    sum += r.score;
                    count++;
                }
            }
        }
        return count > 0 ? (float)(sum / count) : 0f;
    }

    /// <summary>지정 날짜·시간대에 해당하는 기록 중 가장 이른 것. 없으면 null.</summary>
    public static BrushingRecord EarliestInSlotOnDate(IList<BrushingRecord> records, string slot, DateTime date)
    {
        BrushingRecord best = null;
        DateTime bestTime = DateTime.MaxValue;
        if (records != null)
        {
            foreach (var r in records)
            {
                if (r == null || r.timeSlot != slot) continue;
                var t = LocalTimeOf(r);
                if (t.Date != date.Date) continue;
                if (t < bestTime) { bestTime = t; best = r; }
            }
        }
        return best;
    }

    /// <summary>
    /// 최근 windowDays 기록에서 '가장 부족한(평균 커버리지가 가장 낮은) 구역'을 찾고,
    /// 그 구역이 최근 며칠 연속(기록이 있는 날 기준) 부족했는지 센다.
    /// 커버리지 = zoneSeconds[z] / (sessionSeconds/5), 0~1로 클램프.
    /// </summary>
    public static ZoneAlert WorstZone(IList<BrushingRecord> records, DateTime now,
                                      int windowDays = 14, float deficientThreshold = 0.5f)
    {
        var result = new ZoneAlert { zoneIndex = -1 };
        if (records == null) return result;

        const int Z = 5;
        DateTime from = now.Date.AddDays(-(windowDays - 1));

        var window = new List<BrushingRecord>();
        var sum = new double[Z];
        var cnt = new int[Z];
        foreach (var r in records)
        {
            if (r == null) continue;
            var d = LocalTimeOf(r).Date;
            if (d < from || d > now.Date) continue;
            window.Add(r);
            if (r.zoneSeconds == null) continue;
            float per = r.sessionSeconds > 0f ? r.sessionSeconds / Z : 36f;
            for (int z = 0; z < Z && z < r.zoneSeconds.Length; z++)
            {
                sum[z] += Clamp01(r.zoneSeconds[z] / per);
                cnt[z]++;
            }
        }
        if (window.Count == 0) return result;

        int worst = 0;
        float worstAvg = float.MaxValue;
        for (int z = 0; z < Z; z++)
        {
            float a = cnt[z] > 0 ? (float)(sum[z] / cnt[z]) : 0f;
            if (a < worstAvg) { worstAvg = a; worst = z; }
        }
        result.hasData = true;
        result.zoneIndex = worst;
        result.avgCoverage = worstAvg;

        // worst 구역의 '하루 평균 커버리지'를 날짜별로 모아, 최근 날짜부터 연속 부족일 카운트.
        var daySum = new Dictionary<DateTime, float>();
        var dayCnt = new Dictionary<DateTime, int>();
        foreach (var r in window)
        {
            if (r.zoneSeconds == null || worst >= r.zoneSeconds.Length) continue;
            float per = r.sessionSeconds > 0f ? r.sessionSeconds / Z : 36f;
            float cov = Clamp01(r.zoneSeconds[worst] / per);
            var day = LocalTimeOf(r).Date;
            daySum[day] = (daySum.TryGetValue(day, out var s) ? s : 0f) + cov;
            dayCnt[day] = (dayCnt.TryGetValue(day, out var c) ? c : 0) + 1;
        }
        var days = new List<DateTime>(daySum.Keys);
        days.Sort();
        days.Reverse();   // 최근 → 과거
        int streak = 0;
        foreach (var day in days)
        {
            float dayAvg = daySum[day] / dayCnt[day];
            if (dayAvg < deficientThreshold) streak++;
            else break;
        }
        result.deficientDays = streak;
        return result;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}

/// <summary>가장 부족한 구역 분석 결과(메인 메뉴 Alert report용).</summary>
public struct ZoneAlert
{
    public bool hasData;       // 윈도우 내 기록이 있는지
    public int zoneIndex;      // 가장 부족한 구역(0~4, UiZone). 데이터 없으면 -1
    public float avgCoverage;  // 그 구역의 평균 커버리지(0~1)
    public int deficientDays;  // 최근부터 연속으로 부족했던 '기록이 있는 날' 수(0이면 양호)
}
