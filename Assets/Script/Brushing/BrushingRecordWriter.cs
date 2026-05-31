using System;
using UnityEngine;

/// <summary>
/// 양치 세션 종료(BrushingSessionCompletedEvent) → 점수 산출 → 기록 저장(JSON) → 결과 이벤트 발행.
/// EventBus만 구독하므로 BrushingSession/UI와 직접 결합하지 않는다.
/// 저장소는 IBrushingRecordRepository로 추상화 — 기본은 JSON, 향후 DB로 교체 가능.
///
/// BrushingSession과 같은 GameObject(BrushingPipelineRig)에 함께 부착한다.
/// </summary>
public class BrushingRecordWriter : MonoBehaviour
{
    [Tooltip("켜면 저장된 기록/점수를 콘솔에 출력 — 검증용.")]
    [SerializeField] private bool logRecords = true;

    // 저장소 — 기본 JSON. 테스트/DB 전환 시 SetRepository로 주입.
    private IBrushingRecordRepository _repository;

    private void Awake()
    {
        if (_repository == null)
            _repository = new JsonBrushingRecordRepository();
    }

    private void OnEnable()  => EventBus.Subscribe<BrushingSessionCompletedEvent>(OnCompleted);
    private void OnDisable() => EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnCompleted);

    /// <summary>저장소 교체 지점(DB 구현 주입/테스트용). Awake 전·후 모두 허용.</summary>
    public void SetRepository(IBrushingRecordRepository repository)
    {
        if (repository != null) _repository = repository;
    }

    /// <summary>세션 종료 → 점수 산출 + 레코드 저장 + 결과 이벤트 발행.</summary>
    private void OnCompleted(BrushingSessionCompletedEvent e)
    {
        // 1) 점수 산출. (구역당 목표 = targetPerZone, 지속성 = 가장 길게 쉰 시간)
        var result = BrushingScorer.Compute(e.zoneElapsed, e.targetPerZone, e.longestIdleSeconds);

        // 2) 기록 레코드 구성 — 진행 시각 + 최종 점수 포함.
        var now = DateTimeOffset.Now;
        var record = new BrushingRecord
        {
            // id는 현재 단계에서 비워둔다(DB 전환 시 PK 발급). 정렬은 recordedAtUnix 사용.
            recordedAt           = now.ToString("o"),
            recordedAtUnix       = now.ToUnixTimeSeconds(),
            score                = result.score,
            totalBrushingSeconds = e.totalElapsed,
            sessionSeconds       = e.targetTotal,
            longestIdleSeconds   = e.longestIdleSeconds,
            zoneSeconds          = e.zoneElapsed != null ? (float[])e.zoneElapsed.Clone() : new float[0],
            coverage01           = result.coverage01,
            evenness01           = result.evenness01,
            continuity01         = result.continuity01,
        };

        // 3) 저장.
        try
        {
            _repository.Save(record);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BrushingRecord] 저장 실패: {ex.Message}");
        }

        if (logRecords)
            Debug.Log($"[BrushingRecord] 저장 — score={record.score}, " +
                      $"engaged={record.totalBrushingSeconds:F1}s/{record.sessionSeconds:F0}s, " +
                      $"longestIdle={record.longestIdleSeconds:F1}s, at={record.recordedAt}");

        // 4) 결과 이벤트 발행 (리포트 UI 등에서 구독).
        EventBus.Publish(new BrushingScoredEvent
        {
            score                = result.score,
            totalBrushingSeconds = e.totalElapsed,
            sessionSeconds       = e.targetTotal,
            zoneSeconds          = record.zoneSeconds,
            breakdown            = result,
        });
    }
}
