using UnityEngine;

/// <summary>
/// 순차 양치 가이드의 진행 상태머신.
/// BrushingRoutine(구역 순서·시간 데이터)을 읽어, 한 번에 한 구역씩 "차례"를 진행한다.
///   - 현재 차례 구역을 닦는 동안에만 그 단계의 경과 시간이 쌓인다.
///   - 단계 목표 시간을 채우면 다음 구역으로 자동 전환.
///   - 전체 목표 시간(기본 3분)이 지나면 남은 단계 상관없이 종료.
///
/// 입력:  ZoneDetectedEvent (ML 파이프라인이 매 프레임 발행 — 현재 닦는 구역/engagement).
/// 출력:  RoutineStarted / StepStarted / Progress / StepCompleted / Completed (EventBus).
///        UI(하단 대사창·진행바)와 DialogueDirector가 이 이벤트들을 구독한다.
///
/// 기존 BrushingSession(5구역 동시 통계)과 독립적으로 동작 — 같은 ZoneDetectedEvent를
/// 각자 구독한다. 둘을 같은 GameObject(BrushingPipelineRig)에 함께 두어도 무방하다.
/// </summary>
public class BrushingRoutineRunner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Routine Data")]
    [Tooltip("구역 순서·시간을 담은 ScriptableObject. 비워두면 기본 루틴(앞니→좌상→좌하→우상→우하, 3분)으로 폴백.")]
    [SerializeField] private BrushingRoutine routine;

    [Header("Behavior")]
    [Tooltip("씬 활성화(OnEnable) 시 자동으로 루틴 시작. false면 외부에서 StartRoutine() 호출.")]
    [SerializeField] private bool autoStartOnEnable = true;

    [Tooltip("Progress 이벤트 발행 주기(초). 작을수록 UI가 부드럽고, 크면 GC 절약. 0.1초 권장.")]
    [SerializeField, Range(0f, 1f)] private float progressInterval = 0.1f;

    [Tooltip("켜면 단계 전환/진행을 콘솔에 찍는다 — UI 없이 로직 검증용. 검증 후 false.")]
    [SerializeField] private bool logEvents = true;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // 실제 사용하는 루틴(인스펙터 지정 또는 기본 폴백). StartRoutine에서 확정.
    private BrushingRoutine activeRoutine;

    // 현재 단계 인덱스(0-based).
    private int stepIndex;

    // 현재 단계에서 "대상 구역을 실제로 닦은" 누적 시간(초).
    private float stepElapsed;

    // 세션 전체 경과 시간(초) — 단계와 무관하게 흐른다(3분 캡 판정용).
    private float totalElapsed;

    // 루틴 활성 상태. false면 Update가 일찍 return.
    private bool isActive;

    // 다음 Progress 이벤트 발행 시각. throttle용.
    private float nextProgressTime;

    // 모든 단계가 각자 목표를 채웠는지 누적 추적(중간에 시간 부족으로 넘어가면 false).
    private bool allStepsReached;

    // 가장 최근 ZoneDetectedEvent에서 받은 현재 닦는 구역들(5-zone, 1~2개) / engagement.
    private UiZone[] latestUiZones = System.Array.Empty<UiZone>();
    private bool latestEngaged;

    // ─────────────────────────────────────────────────────────────
    // 외부 읽기 전용 상태 (UI가 이벤트 대신 직접 폴링하고 싶을 때)
    // ─────────────────────────────────────────────────────────────

    /// <summary>루틴 진행 중 여부.</summary>
    public bool IsActive => isActive;

    /// <summary>현재 단계 인덱스(0-based).</summary>
    public int CurrentStepIndex => stepIndex;

    /// <summary>현재 단계의 대상 구역(루틴 비어 있으면 Front).</summary>
    public UiZone CurrentTargetZone =>
        (activeRoutine != null && stepIndex < activeRoutine.StepCount)
            ? activeRoutine.ZoneAt(stepIndex)
            : UiZone.Front;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus.Subscribe<ZoneDetectedEvent>(OnZone);
        if (autoStartOnEnable) StartRoutine();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZone);

        // 진행 중에 비활성화되면 마지막 결과라도 발행해 구독자 상태를 정리.
        if (isActive) CompleteRoutine();
    }

    private void Update()
    {
        if (!isActive) return;

        float dt = Time.deltaTime;

        // 양치 중(engaged)일 때만 시간이 흐른다 — 손을 떼면 전체 시간도 멈춘다.
        // 진행바는 이 totalElapsed 기준이라, 양치를 멈추면 진행바도 멈춘다.
        if (latestEngaged)
        {
            totalElapsed += dt;

            // 그중에서도 현재 차례 구역을 실제로 닦을 때만 단계 시간 가산.
            if (IsBrushingTarget())
            {
                stepElapsed += dt;
            }
        }

        // Progress 이벤트 발행(throttle).
        if (Time.time >= nextProgressTime)
        {
            nextProgressTime = Time.time + progressInterval;
            PublishProgress();
        }

        // 단계 목표 도달 → 다음 단계로.
        if (stepElapsed >= CurrentStepTargetSeconds())
        {
            AdvanceStep(reachedTarget: true);
            return; // AdvanceStep 안에서 종료될 수 있으니 이번 프레임은 여기서 마무리.
        }

        // 전체 시간 소진 → 남은 단계 상관없이 종료.
        if (totalElapsed >= activeRoutine.TotalSeconds)
        {
            CompleteRoutine();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 루틴 시작 — 타이머/단계를 0에서 초기화하고 Started + 첫 StepStarted 이벤트 발행.
    /// 씬 진입 시 자동 호출(autoStartOnEnable)되며, 외부에서 재시작용으로 호출해도 된다.
    /// </summary>
    public void StartRoutine()
    {
        // 인스펙터 미지정이면 코드 기본 루틴으로 폴백.
        activeRoutine = routine != null ? routine : BrushingRoutine.CreateDefault();

        stepIndex = 0;
        stepElapsed = 0f;
        totalElapsed = 0f;
        nextProgressTime = 0f;     // 다음 Update에서 즉시 첫 Progress 발행.
        allStepsReached = true;    // 단계 중 하나라도 미달이면 false로 떨어진다.
        latestUiZones = System.Array.Empty<UiZone>();
        latestEngaged = false;
        isActive = true;

        if (activeRoutine.StepCount == 0)
        {
            Debug.LogWarning("[Routine] 단계가 0개 — 즉시 종료한다. BrushingRoutine.steps를 확인하라.");
            isActive = false;
            return;
        }

        if (logEvents)
            Debug.Log($"[Routine] Start — {activeRoutine.StepCount}단계, total={activeRoutine.TotalSeconds:F0}s");

        EventBus.Publish(new RoutineStartedEvent
        {
            stepCount = activeRoutine.StepCount,
            totalSeconds = activeRoutine.TotalSeconds,
        });

        PublishStepStarted();
    }

    /// <summary>
    /// 루틴을 즉시 종료하고 Completed 이벤트를 발행한다. 이미 종료 상태면 no-op.
    /// 자동 종료(전체 시간 소진/모든 단계 완료)와 수동 종료(외부 버튼) 모두 이 메서드를 거친다.
    /// </summary>
    public void CompleteRoutine()
    {
        if (!isActive) return;
        isActive = false;

        if (logEvents)
            Debug.Log($"[Routine] Complete — total={totalElapsed:F1}s, allReached={allStepsReached}");

        EventBus.Publish(new RoutineCompletedEvent
        {
            totalElapsed = totalElapsed,
            totalSeconds = activeRoutine.TotalSeconds,
            allStepsReachedTarget = allStepsReached,
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────

    /// <summary>ZoneDetectedEvent 핸들러 — 최신 구역/engagement만 보관. 시간 가산은 Update에서.</summary>
    private void OnZone(ZoneDetectedEvent e)
    {
        latestUiZones = e.uiZones ?? System.Array.Empty<UiZone>();
        latestEngaged = e.isEngaged;
    }

    /// <summary>현재 닦는 구역(들)에 이번 단계의 대상 구역이 포함되는가.</summary>
    private bool IsBrushingTarget()
    {
        var target = CurrentTargetZone;
        for (int i = 0; i < latestUiZones.Length; i++)
        {
            if (latestUiZones[i] == target) return true;
        }
        return false;
    }

    /// <summary>현재 단계의 목표 시간(초).</summary>
    private float CurrentStepTargetSeconds() => activeRoutine.SecondsAt(stepIndex);

    /// <summary>
    /// 현재 단계를 종료하고 다음 단계로 전환한다.
    /// 마지막 단계였다면 루틴 전체를 Complete한다.
    /// </summary>
    private void AdvanceStep(bool reachedTarget)
    {
        if (!reachedTarget) allStepsReached = false;

        if (logEvents)
            Debug.Log($"[Routine] Step {stepIndex} '{CurrentTargetZone}' 완료 — " +
                      $"{stepElapsed:F1}/{CurrentStepTargetSeconds():F1}s, reached={reachedTarget}");

        EventBus.Publish(new RoutineStepCompletedEvent
        {
            stepIndex = stepIndex,
            targetZone = CurrentTargetZone,
            stepElapsed = stepElapsed,
            stepTargetSeconds = CurrentStepTargetSeconds(),
            reachedTarget = reachedTarget,
        });

        // 다음 단계로.
        stepIndex++;
        stepElapsed = 0f;

        if (stepIndex >= activeRoutine.StepCount)
        {
            CompleteRoutine();
            return;
        }

        PublishStepStarted();
    }

    /// <summary>현재 단계 시작 이벤트 발행.</summary>
    private void PublishStepStarted()
    {
        if (logEvents)
            Debug.Log($"[Routine] Step {stepIndex}/{activeRoutine.StepCount} 시작 — " +
                      $"'{CurrentTargetZone}', 목표 {CurrentStepTargetSeconds():F0}s");

        bool hasNext = stepIndex + 1 < activeRoutine.StepCount;

        EventBus.Publish(new RoutineStepStartedEvent
        {
            stepIndex = stepIndex,
            stepCount = activeRoutine.StepCount,
            targetZone = CurrentTargetZone,
            stepTargetSeconds = CurrentStepTargetSeconds(),
            hasNext = hasNext,
            nextZone = hasNext ? activeRoutine.ZoneAt(stepIndex + 1) : CurrentTargetZone,
        });
    }

    /// <summary>진행 상황 이벤트 발행 — UI 진행바/타이머/대사 갱신용.</summary>
    private void PublishProgress()
    {
        bool onTarget = latestEngaged && IsBrushingTarget();

        // 현재 닦는 구역 대표값 — 대상과 일치하면 대상, 아니면 닦는 것 중 첫 번째, 미검출이면 null.
        UiZone? currentZone = null;
        if (latestEngaged && latestUiZones.Length > 0)
        {
            currentZone = onTarget ? CurrentTargetZone : latestUiZones[0];
        }

        EventBus.Publish(new RoutineProgressEvent
        {
            stepIndex = stepIndex,
            targetZone = CurrentTargetZone,
            stepElapsed = stepElapsed,
            stepTargetSeconds = CurrentStepTargetSeconds(),
            totalElapsed = totalElapsed,
            totalSeconds = activeRoutine.TotalSeconds,
            currentBrushingZone = currentZone,
            isOnTarget = onTarget,
        });
    }
}
