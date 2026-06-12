using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빌드/에디터 양쪽에서 최근 3개월치 더미 양치 기록을 채워 넣는 시더.
/// 아이폰 등 실기기 빌드에서도 리포트/캘린더/홈 점수가 비어 보이지 않게 하기 위한 테스트용.
///
/// 동작(앱 시작 시 1회): 저장된 양치 기록이 하나도 없으면 최근 90일치 더미 기록을 생성해 저장한다.
/// 기록의 childId는 빈 값으로 둔다 — BrushingStats.ForChild가 '첫 아이' 것으로 귀속하므로,
/// 온보딩에서 아이를 '하준'으로 만들면 그 데이터가 하준 리포트에 그대로 표시된다.
///
/// ※ 아이 프로필/온보딩은 손대지 않는다 — 온보딩 화면이 정상적으로 떠서, 사용자가 직접 '하준'을
///    입력해 아이를 만든다. (예전엔 여기서 온보딩을 자동 완료시켜 화면이 안 떴다 — 제거함.)
///
/// 양치 기록 파일은 persistentDataPath에 영속되므로, 한 번 시드되면 다음 실행부터는 그대로 재사용된다.
///
/// ⚠ 운영 빌드에서는 <see cref="Enabled"/>를 false로 바꿔 끈다(또는 이 파일을 제외).
/// </summary>
public static class DummyDataSeeder
{
    /// <summary>더미 시드 사용 여부. 운영 배포 시 false로.</summary>
    private const bool Enabled = true;

    private const int DaysBack = 90;          // 약 3개월
    private const int RandomSeed = 20260612;  // 재현성 있는 더미 분포용 고정 시드

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Seed()
    {
        if (!Enabled) return;

        try
        {
            SeedRecordsIfNeeded();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DummyDataSeeder] 시드 실패: {e.Message}");
        }
    }

    // 저장된 기록이 없을 때만 최근 90일치 더미 기록을 생성·저장한다.
    private static void SeedRecordsIfNeeded()
    {
        var repo = new JsonBrushingRecordRepository();
        if (repo.LoadAll().Count > 0) return;   // 이미 기록(실데이터 포함)이 있으면 시드하지 않는다.

        var rng = new System.Random(RandomSeed);
        var records = new List<BrushingRecord>();
        DateTime today = DateTime.Now.Date;

        for (int d = DaysBack - 1; d >= 0; d--)
        {
            DateTime day = today.AddDays(-d);

            // 현실감: 약 12% 정도는 아예 양치 안 한 날로 비운다(연속일수/캘린더에 빈 칸 생기게).
            if (rng.NextDouble() < 0.12) continue;

            // 아침은 거의 항상, 저녁은 자주, 점심은 가끔.
            AddRecordIf(records, rng, day, hour: 7, prob: 0.92);
            AddRecordIf(records, rng, day, hour: 13, prob: 0.30);
            AddRecordIf(records, rng, day, hour: 21, prob: 0.78);
        }

        repo.SaveAll(records);
        Debug.Log($"[DummyDataSeeder] 더미 양치 기록 {records.Count}건({DaysBack}일치) 시드 완료 → {repo.FilePath}");
    }

    private static void AddRecordIf(List<BrushingRecord> list, System.Random rng, DateTime day, int hour, double prob)
    {
        if (rng.NextDouble() > prob) return;

        // 분 단위 약간의 흔들림 — 같은 시각 중복을 피하고 자연스럽게.
        int minute = rng.Next(0, 60);
        var dto = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeOffset.Now.Offset);

        // 점수 60~98, 살짝 우상향 트렌드(최근일수록 평균이 조금 높게).
        float recencyBoost = (float)(day - DateTime.Now.Date.AddDays(-DaysBack)).TotalDays / DaysBack; // 0~1
        int baseScore = 64 + (int)(recencyBoost * 12);   // 64~76
        int score = Mathf.Clamp(baseScore + rng.Next(-8, 16), 50, 99);

        // 5구역(0=위왼,1=위오른,2=아래왼,3=아래오른,4=앞니) 누적 시간. 합 ≈ totalBrushing.
        float total = 22f + (float)rng.NextDouble() * 8f;   // 22~30초
        float[] zones = DistributeZones(rng, total);

        list.Add(new BrushingRecord
        {
            childId              = "",                         // 빈 값 → 첫 아이(하준)에 귀속.
            recordedAt           = dto.ToString("o"),
            recordedAtUnix       = dto.ToUnixTimeSeconds(),
            timeSlot             = BrushingTimeSlot.Korean(dto),
            score                = score,
            totalBrushingSeconds = total,
            sessionSeconds       = 30f,
            longestIdleSeconds   = (float)rng.NextDouble() * 4f,
            zoneSeconds          = zones,
            coverage01           = Mathf.Clamp01(score / 100f + (float)(rng.NextDouble() - 0.5) * 0.1f),
            evenness01           = Mathf.Clamp01(0.7f + (float)(rng.NextDouble() - 0.5) * 0.3f),
            continuity01         = Mathf.Clamp01(0.75f + (float)(rng.NextDouble() - 0.5) * 0.3f),
        });
    }

    // 총 시간을 5구역에 분배 — 균등 기반 + 좌/우 또는 상/하로 약간 치우치게 해 균형 리포트가 다양하게 보이도록.
    private static float[] DistributeZones(System.Random rng, float total)
    {
        float[] w = new float[5];
        for (int i = 0; i < 5; i++) w[i] = 0.8f + (float)rng.NextDouble() * 0.4f; // 0.8~1.2 기본 가중치

        // 절반의 확률로 한 방향(좌/우/상/하)을 더 닦은 것처럼 가중.
        switch (rng.Next(0, 5))
        {
            case 0: w[0] *= 1.5f; w[2] *= 1.5f; break; // 왼쪽 강조
            case 1: w[1] *= 1.5f; w[3] *= 1.5f; break; // 오른쪽 강조
            case 2: w[0] *= 1.4f; w[1] *= 1.4f; break; // 윗니 강조
            case 3: w[2] *= 1.4f; w[3] *= 1.4f; break; // 아랫니 강조
            default: break;                             // 균형
        }

        float sum = 0f;
        for (int i = 0; i < 5; i++) sum += w[i];
        float[] zones = new float[5];
        for (int i = 0; i < 5; i++) zones[i] = total * (w[i] / sum);
        return zones;
    }
}
