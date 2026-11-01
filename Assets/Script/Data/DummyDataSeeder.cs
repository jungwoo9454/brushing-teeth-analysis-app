using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빌드/에디터 양쪽에서 <b>오늘 날짜 기준</b> 최근 3개월치 더미 양치 기록을 채워 넣는 시더.
/// 아이폰 등 실기기 빌드에서도 리포트/캘린더/홈 점수·부족구역 알림이 비어 보이지 않게 하기 위한 테스트용.
///
/// 동작(앱 시작 시 1회):
///   1) 이 <see cref="SeedVersion"/>으로 이미 시드했으면(=PlayerPrefs 기록) 아무것도 하지 않는다.
///   2) 실제 사용자 기록(childId가 채워진)이 하나라도 있으면 덮어쓰지 않는다(안전장치).
///   3) 그 외에는 <see cref="DateTime.Now"/> 오늘을 기준으로 최근 90일치 더미 기록을 생성해 통째로 저장한다.
///
/// ▷ "오늘 기준으로 다시 만들고 싶다"면 <see cref="SeedVersion"/> 값을 1 올리면 된다 —
///    다음 실행에서 옛 더미를 지우고 오늘 기준으로 딱 한 번 재생성한다(실데이터가 없을 때만).
///
/// 기록의 childId는 빈 값으로 둔다 — BrushingStats.ForChild가 '첫 아이' 것으로 귀속하므로,
/// 온보딩에서 아이를 '하준'으로 만들면 그 데이터가 하준 리포트에 그대로 표시된다.
///
/// ※ 아이 프로필/온보딩은 손대지 않는다 — 온보딩 화면이 정상적으로 떠서, 사용자가 직접 아이를 만든다.
///
/// 다양성(리포트가 밋밋하지 않게):
///   · 완만한 우상향 실력 + 주 단위 기복(sin) + 2주 전쯤 '슬럼프' 구간
///   · 주말은 아침 양치 확률↓·점심↑, 결석(빈 날)도 더 잦게
///   · 하루 1~3회(아침/점심/저녁) 가변, 점수 40~99로 넓게 분포
///   · 최근 열흘은 '오른쪽 아랫니'를 계속 소홀 → 홈 화면 부족구역 알림/스트릭이 뜨도록,
///     그 이전은 주마다 소홀 구역을 회전시켜 좌우/상하 균형 리포트가 다양해 보이게
///   · 세션 길이는 현재 양치 루틴(1분)과 일치: sessionSeconds=60, 구역당 목표 12초
///
/// ⚠ 운영 빌드에서는 <see cref="Enabled"/>를 false로 바꿔 끈다(또는 이 파일을 제외).
/// </summary>
public static class DummyDataSeeder
{
    /// <summary>더미 시드 사용 여부. 운영 배포 시 false로.</summary>
    private const bool Enabled = true;

    private const int DaysBack = 90;          // 약 3개월
    private const int RandomSeed = 20260808;  // 재현성 있는 더미 분포용 고정 시드(오늘 기준)

    /// <summary>
    /// 시드 버전. 이 값을 올리면 다음 실행에서 옛 더미를 지우고 오늘 기준으로 재생성한다(실데이터 없을 때만).
    /// </summary>
    private const int SeedVersion = 2;
    private const string SeedVersionKey = "DummyDataSeeder.SeedVersion";

    private const float SessionSeconds = 60f;             // 현재 양치 루틴 길이(1분)와 일치
    private const float PerZone = SessionSeconds / 5f;     // 구역당 목표(12초) — 참고용

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Seed()
    {
        if (!Enabled) return;

        try
        {
            SeedIfNeeded();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DummyDataSeeder] 시드 실패: {e.Message}");
        }
    }

    private static void SeedIfNeeded()
    {
        // 1) 이 버전으로 이미 시드했으면 매 실행마다 재생성하지 않는다.
        if (PlayerPrefs.GetInt(SeedVersionKey, 0) >= SeedVersion) return;

        var repo = new JsonBrushingRecordRepository();
        var existing = repo.LoadAll();

        // 2) 실제 사용자 기록(childId가 채워진)이 하나라도 있으면 덮어쓰지 않는다.
        //    (더미는 childId="" 이므로, 실데이터가 섞이면 보존한다.)
        bool anyReal = existing.Exists(r => r != null && !string.IsNullOrEmpty(r.childId));
        if (anyReal)
        {
            PlayerPrefs.SetInt(SeedVersionKey, SeedVersion);
            PlayerPrefs.Save();
            Debug.Log("[DummyDataSeeder] 실제 사용자 기록 감지 — 더미 재시드 건너뜀(기존 데이터 보존).");
            return;
        }

        // 3) 오늘 기준으로 새로 생성해 통째로 저장(옛 더미는 대체).
        var records = BuildRecords();
        repo.SaveAll(records);

        PlayerPrefs.SetInt(SeedVersionKey, SeedVersion);
        PlayerPrefs.Save();

        Debug.Log($"[DummyDataSeeder] 오늘({DateTime.Now.Date:yyyy-MM-dd}) 기준 더미 양치 기록 " +
                  $"{records.Count}건({DaysBack}일치) 시드 완료 → {repo.FilePath}");
    }

    // 오늘을 마지막 날로 두고 최근 90일치 더미 기록을 생성한다.
    private static List<BrushingRecord> BuildRecords()
    {
        var rng = new System.Random(RandomSeed);
        var records = new List<BrushingRecord>();
        DateTime today = DateTime.Now.Date;

        for (int d = DaysBack - 1; d >= 0; d--)
        {
            DateTime day = today.AddDays(-d);
            int idx = (DaysBack - 1) - d;                       // 0 = 가장 과거 ... DaysBack-1 = 오늘
            float progress = DaysBack > 1 ? (float)idx / (DaysBack - 1) : 1f;   // 0~1
            bool weekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;

            // 그날의 '실력' 0~1 — 완만한 우상향 + 주 단위 기복 + 2주 전쯤 슬럼프 + 약간의 소음.
            float quality = 0.55f + 0.22f * progress;
            quality += 0.06f * Mathf.Sin(idx / 7f * Mathf.PI * 2f);   // 주 단위 오르내림
            if (d >= 12 && d <= 19) quality -= 0.16f;                 // 2주 전쯤 슬럼프 구간
            quality += (float)(rng.NextDouble() - 0.5) * 0.08f;       // 소음
            quality = Mathf.Clamp(quality, 0.20f, 0.96f);

            // 결석(양치 안 한 빈 날): 주말·저품질일수록 잦게 → 캘린더/연속일수에 빈 칸이 자연스럽게.
            float skipProb = (weekend ? 0.16f : 0.07f) + (1f - quality) * 0.10f;
            if (rng.NextDouble() < skipProb) continue;

            // 최근 열흘은 '오른쪽 아랫니(zone3)'를 계속 소홀 → 홈 부족구역 알림/연속 부족일이 뜨도록.
            // 그 이전은 주마다 소홀 구역을 회전시켜 좌우/상하 균형 리포트가 다양해 보이게.
            int neglect = d <= 9 ? 3 : (idx / 7) % 5;

            float morningProb = weekend ? 0.72f : 0.94f;   // 주말엔 늦잠 → 아침 양치 확률↓
            float lunchProb   = weekend ? 0.42f : 0.24f;   // 주말 점심 양치는 조금 더 자주
            float eveningProb = 0.82f;

            AddSession(records, rng, day, hour: 7,  prob: morningProb, quality: quality, neglect: neglect);
            AddSession(records, rng, day, hour: 13, prob: lunchProb,   quality: quality, neglect: neglect);
            AddSession(records, rng, day, hour: 20, prob: eveningProb, quality: quality, neglect: neglect);
        }

        return records;
    }

    private static void AddSession(List<BrushingRecord> list, System.Random rng, DateTime day,
                                   int hour, float prob, float quality, int neglect)
    {
        if (rng.NextDouble() > prob) return;

        // 분 단위 흔들림 — 같은 시각 중복을 피하고 자연스럽게.
        int minute = rng.Next(0, 55);
        var dto = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeOffset.Now.Offset);

        // 점수: 실력 기반 + 소음 → 대략 40~99로 넓게 분포(슬럼프 날은 더 낮게).
        int score = Mathf.Clamp(Mathf.RoundToInt(quality * 100f) + rng.Next(-9, 8), 38, 99);

        // 실제 양치한 시간(초): 실력 좋을수록 오래(약 24~59초). 세션 창은 60초.
        float total = Mathf.Clamp(26f + quality * 30f + (float)(rng.NextDouble() - 0.5) * 8f, 20f, 59f);

        // 가장 길게 쉰 시간: 실력 나쁠수록 김(0~14초).
        float idle = Mathf.Clamp((1f - quality) * 7f + (float)rng.NextDouble() * 3f, 0f, 14f);

        float[] zones = DistributeZones(rng, total, neglect, quality);

        // coverage/evenness/continuity(표시·디버그용) — 실제 데이터와 대략 일관되게.
        float coverage = Mathf.Clamp01(total / SessionSeconds);
        float minZ = float.MaxValue, maxZ = 0f;
        foreach (var z in zones) { if (z < minZ) minZ = z; if (z > maxZ) maxZ = z; }
        float evenness = maxZ > 0f ? Mathf.Clamp01(minZ / maxZ) : 0f;
        float continuity = Mathf.Clamp01(1f - idle / 12f);

        list.Add(new BrushingRecord
        {
            childId              = "",                     // 빈 값 → 첫 아이에 귀속.
            recordedAt           = dto.ToString("o"),
            recordedAtUnix       = dto.ToUnixTimeSeconds(),
            timeSlot             = BrushingTimeSlot.Korean(dto),
            score                = score,
            totalBrushingSeconds = total,
            sessionSeconds       = SessionSeconds,
            longestIdleSeconds   = idle,
            zoneSeconds          = zones,
            coverage01           = coverage,
            evenness01           = evenness,
            continuity01         = continuity,
        });
    }

    // 총 시간을 5구역(0=위왼,1=위오른,2=아래왼,3=아래오른,4=앞니)에 분배.
    // 소홀 구역은 확 낮추고, 실력 좋은 날은 전체적으로 더 고르게(균형 리포트가 다양하게 보이도록).
    private static float[] DistributeZones(System.Random rng, float total, int neglect, float quality)
    {
        float[] w = new float[5];
        for (int i = 0; i < 5; i++) w[i] = 0.85f + (float)rng.NextDouble() * 0.30f;   // 0.85~1.15 기본

        // 실력 좋은 날은 편차를 줄여 고르게(먼저 적용).
        if (quality > 0.80f)
            for (int i = 0; i < 5; i++) w[i] = Mathf.Lerp(w[i], 1f, 0.40f);

        // 소홀 구역은 마지막에 확 낮춰 항상 유지되게(부족구역 알림/균형 리포트가 살아나도록).
        if (neglect >= 0 && neglect < 5) w[neglect] *= 0.35f;

        float sum = 0f;
        for (int i = 0; i < 5; i++) sum += w[i];

        float[] zones = new float[5];
        for (int i = 0; i < 5; i++) zones[i] = total * (w[i] / sum);
        return zones;
    }
}
