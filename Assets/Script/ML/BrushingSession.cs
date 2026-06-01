using UnityEngine;

/// <summary>
/// 양치 세션 동안 5-zone별 누적 시간을 측정·관리하는 컴포넌트.
/// ZoneDetectedEvent를 구독해 engaged=true이고 stableZone != None인 프레임에서
/// 해당 zone의 UI 매핑(1~2개의 UiZone)에 Time.deltaTime을 가산한다.
///
/// 세션 lifecycle:
///   - StartSession: 카운터 리셋 + BrushingSessionStartedEvent 발행
///   - 매 프레임 시간 누적
///   - tickInterval마다 BrushingSessionTickEvent 발행 (UI 갱신 주기)
///   - HasAchievedTarget이 되면(autoEndOnTargetReached=true일 때) 자동 EndSession
///   - EndSession: BrushingSessionCompletedEvent 발행
///
/// 양치 씬의 BrushingPipelineRig에 함께 부착하여 사용.
/// </summary>
[RequireComponent(typeof(BrushingPipeline))]
public class BrushingSession : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Targets")]
    [Tooltip("권장 전체 양치 시간 (초). 일반 가이드 = 3분 = 180초.")]
    [SerializeField] private float targetTotalSeconds = 180f;

    [Tooltip("zone당 권장 시간 (초). 5-zone 균등이면 targetTotalSeconds / 5 = 36초.")]
    [SerializeField] private float targetPerZoneSeconds = 36f;

    [Header("Behavior")]
    [Tooltip("씬 활성화(OnEnable) 시 자동으로 세션 시작. false면 외부에서 StartSession() 호출 필요.")]
    [SerializeField] private bool autoStartOnEnable = true;

    [Tooltip("모든 zone이 zone별 목표에 도달하고 전체 시간도 목표 이상이면 자동 종료. false면 외부 EndSession() 호출 필요.")]
    [SerializeField] private bool autoEndOnTargetReached = true;

    [Tooltip("세션 시작 후 이 시간(초, 벽시계 기준)이 지나면 무조건 자동 종료. 3분 = 180초. 0이면 시간 기반 자동 종료 안 함.")]
    [SerializeField] private float autoEndAfterSeconds = 180f;

    [Tooltip("Tick 이벤트 발행 주기 (초). 작을수록 UI 부드러움, 크면 GC 절약. 0.1초 권장.")]
    [SerializeField, Range(0f, 1f)] private float tickInterval = 0.1f;

    [Tooltip("true면 세션 시작/종료/Tick을 콘솔에 찍는다. 검증 후 false.")]
    [SerializeField] private bool logSessionEvents = false;

    [Header("디버그 (임시)")]
    [Tooltip("켜면 디버그 스킵 키로 진행 중인 세션을 즉시 종료할 수 있다.")]
    [SerializeField] private bool debugAllowSkip = true;

    [Tooltip("이 키를 누르면 세션을 즉시 종료한다(현재까지 누적으로 Completed 발행). 디버그용.")]
    [SerializeField] private KeyCode debugSkipKey = KeyCode.Return;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    /// <summary>UiZone enum의 5개 값. 외부 변경 방지를 위해 const처럼 사용.</summary>
    private const int ZONE_COUNT = 5;

    // 5-zone별 누적 시간 (초). UiZone index와 1:1 매핑.
    // 매 Tick 발행 시 복사본을 만들어 구독자가 안전하게 읽을 수 있게 한다.
    private readonly float[] zoneElapsed = new float[ZONE_COUNT];

    // 전체 양치 누적 시간 (engaged 프레임만).
    private float totalElapsed;

    // 세션 활성 상태. false면 Update가 일찍 return.
    private bool isSessionActive;

    // 다음 Tick 이벤트 발행 시각. throttle용.
    private float nextTickTime;

    // 가장 최근 ZoneDetectedEvent의 stableZone — Update에서 시간 가산 + Tick 이벤트에 보고.
    private MlZone latestStable = MlZone.None;

    // 가장 최근 ZoneDetectedEvent의 engaged 플래그.
    private bool latestEngaged;

    // 세션 시작 시각(Time.time) — 벽시계 기준 자동 종료(3분) 판정용.
    private float sessionStartTime;

    // 현재 연속으로 양치를 쉰 시간(초) / 세션 중 가장 길었던 연속 쉼(초). 점수 '지속성' 평가용.
    private float currentIdleRun;
    private float longestIdleRun;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // EventBus 구독 — 핸들러 누수를 막기 위해 OnDisable에서 반드시 Unsubscribe.
        EventBus.Subscribe<ZoneDetectedEvent>(OnZone);

        if (autoStartOnEnable) StartSession();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZone);

        // 세션 진행 중에 컴포넌트가 비활성화되면 마지막 결과라도 발행.
        if (isSessionActive) EndSession();
    }

    private void Update()
    {
        if (!isSessionActive) return;

        // 디버그 — 지정 키를 누르면 목표와 무관하게 즉시 종료(현재까지 누적으로 Completed 발행). 임시/디버그용.
        if (debugAllowSkip && Input.GetKeyDown(debugSkipKey))
        {
            if (logSessionEvents) Debug.Log("[Session] 디버그 스킵 — 즉시 종료");
            EndSession();
            return;
        }

        float dt = Time.deltaTime;

        // engaged + stableZone != None 일 때만 양치 시간 가산, 아니면 '쉰 시간' 누적.
        // engagement 게이트는 이미 BrushingPipeline에서 적용된 값(latestEngaged) 사용.
        if (latestEngaged && latestStable != MlZone.None)
        {
            totalElapsed += dt;
            currentIdleRun = 0f;          // 양치 중 → 연속 쉼 끊김.

            // stableZone(ML 8-class)을 UiZone(5-class)으로 매핑해 1~2개 zone에 가산.
            // SideLeft → UpperLeft + LowerLeft 같이 두 zone에 동시 가산되는 케이스 포함.
            var uiZones = UIZoneMapper.Map(latestStable);
            for (int i = 0; i < uiZones.Length; i++)
            {
                int idx = (int)uiZones[i];
                zoneElapsed[idx] += dt;
            }
        }
        else
        {
            // 양치를 쉬는 중 — 연속 쉼 시간 누적 + 최장 기록 갱신(점수 '지속성' 평가용).
            currentIdleRun += dt;
            if (currentIdleRun > longestIdleRun) longestIdleRun = currentIdleRun;
        }

        // Tick 이벤트 발행 (throttle). UI가 카운터를 갱신할 수 있도록.
        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickInterval;
            PublishTick();
        }

        // 자동 종료 1) 세션 시작 후 일정 시간(기본 3분) 경과 — 벽시계 기준.
        if (autoEndAfterSeconds > 0f && Time.time - sessionStartTime >= autoEndAfterSeconds)
        {
            EndSession();
            return;
        }

        // 자동 종료 2) 모든 zone이 목표 도달 + 전체 시간 목표 이상(옵션).
        if (autoEndOnTargetReached && HasAchievedTarget())
        {
            EndSession();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 세션 시작 — 카운터/타이머 리셋 + Started 이벤트 발행.
    /// 이미 진행 중인 세션이 있어도 강제로 리셋하므로 호출자가 명시 의도를 가져야 한다.
    /// </summary>
    public void StartSession()
    {
        // 5-zone 카운터 초기화. Array.Clear가 가장 간결하지만 명시성을 위해 루프 사용.
        for (int i = 0; i < ZONE_COUNT; i++) zoneElapsed[i] = 0f;
        totalElapsed = 0f;
        nextTickTime = 0f;       // 다음 Update에서 즉시 첫 Tick 발행되도록.
        isSessionActive = true;
        latestStable = MlZone.None;
        latestEngaged = false;
        sessionStartTime = Time.time;   // 벽시계 기준 3분 자동 종료의 기준 시각.
        currentIdleRun = 0f;
        longestIdleRun = 0f;

        if (logSessionEvents)
            Debug.Log($"[Session] Start — targetTotal={targetTotalSeconds:F0}s, perZone={targetPerZoneSeconds:F0}s");

        EventBus.Publish(new BrushingSessionStartedEvent
        {
            targetTotal   = targetTotalSeconds,
            targetPerZone = targetPerZoneSeconds,
        });
    }

    /// <summary>
    /// 세션 종료 — Completed 이벤트 발행.
    /// 이미 종료된 상태면 no-op. 자동 종료(목표 도달)와 수동 종료(외부 버튼) 모두 이 메서드를 거친다.
    /// </summary>
    public void EndSession()
    {
        if (!isSessionActive) return;
        isSessionActive = false;

        // 결과 발행 시 zoneElapsed 배열을 복사해 호출자가 변경해도 우리 내부 상태 영향 없게 한다.
        var snapshot = (float[])zoneElapsed.Clone();
        bool achieved = HasAchievedTarget();

        if (logSessionEvents)
            Debug.Log($"[Session] End — total={totalElapsed:F1}s, achieved={achieved}, " +
                      $"zones=[{snapshot[0]:F1},{snapshot[1]:F1},{snapshot[2]:F1},{snapshot[3]:F1},{snapshot[4]:F1}]");

        EventBus.Publish(new BrushingSessionCompletedEvent
        {
            totalElapsed       = totalElapsed,
            zoneElapsed        = snapshot,
            targetTotal        = targetTotalSeconds,
            targetPerZone      = targetPerZoneSeconds,
            achievedTarget     = achieved,
            longestIdleSeconds = longestIdleRun,
        });
    }

    /// <summary>현재 세션이 진행 중인지 (외부 UI/매니저용 read-only).</summary>
    public bool IsActive => isSessionActive;

    /// <summary>현재 전체 누적 시간 (UI에서 직접 폴링 시 사용 — Tick 이벤트 권장).</summary>
    public float TotalElapsed => totalElapsed;

    // ─────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────

    /// <summary>EventBus 핸들러 — 최신 stableZone/engaged를 보관. 시간 가산은 Update에서.</summary>
    private void OnZone(ZoneDetectedEvent e)
    {
        latestStable = e.stableZone;
        latestEngaged = e.isEngaged;
    }

    /// <summary>Tick 이벤트 발행 — zoneElapsed 복사본 동봉으로 외부 mutation 방지.</summary>
    private void PublishTick()
    {
        var snapshot = (float[])zoneElapsed.Clone();
        if (logSessionEvents)
            Debug.Log($"[Session] Tick — total={totalElapsed:F1}s, " +
                      $"UL={snapshot[0]:F1},UR={snapshot[1]:F1},LL={snapshot[2]:F1},LR={snapshot[3]:F1},F={snapshot[4]:F1}, " +
                      $"current={latestStable}, engaged={latestEngaged}");

        EventBus.Publish(new BrushingSessionTickEvent
        {
            totalElapsed  = totalElapsed,
            zoneElapsed   = snapshot,
            targetTotal   = targetTotalSeconds,
            targetPerZone = targetPerZoneSeconds,
            currentZone   = latestStable,
            isEngaged     = latestEngaged,
        });
    }

    /// <summary>
    /// 목표 달성 여부 — 모든 zone이 zone별 목표에 도달 + 전체 시간도 목표 이상.
    /// 자동 종료 조건 + Completed 이벤트의 achievedTarget 필드 산정에 같이 사용.
    /// </summary>
    private bool HasAchievedTarget()
    {
        if (totalElapsed < targetTotalSeconds) return false;
        for (int i = 0; i < ZONE_COUNT; i++)
        {
            if (zoneElapsed[i] < targetPerZoneSeconds) return false;
        }
        return true;
    }
}
