using UnityEngine;

/// <summary>
/// 루틴 진행 상황을 보고 "지금 어떤 대사를 보여줄지" 결정하는 디렉터.
/// 루틴 이벤트(StepStarted/Progress/StepCompleted/Completed)를 구독해
/// 상황별 멘트를 골라 CharacterDialogueEvent로 발행한다.
///
/// 멘트 문구는 인스펙터에서 수정 가능 — "{zone}" 토큰이 구역 한글명으로 치환된다.
/// UI(대사창)는 이 디렉터와 분리되어 있어, 대사창 구현 전에도 logEvents로 동작을 검증할 수 있다.
/// </summary>
public class DialogueDirector : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터에서 수정 가능한 멘트 템플릿 ("{zone}" → 구역명 치환)
    // ─────────────────────────────────────────────────────────────

    [Header("멘트 템플릿 — \"{zone}\"은 구역명으로 치환됨")]
    [Tooltip("새 구역 안내. 예: \"이제 {zone}를 닦아볼까요?\"")]
    [SerializeField] private string guideTemplate = "이제 {zone}를 닦아볼까요?";

    [Tooltip("다른 구역을 닦고 있을 때. 예: \"{zone}를 닦아주세요!\"")]
    [SerializeField] private string correctTemplate = "{zone}를 닦아주세요!";

    [Tooltip("시간이 부족한 채 넘어갈 때. 예: \"{zone}를 조금 더 닦아주세요\"")]
    [SerializeField] private string encourageTemplate = "{zone}를 조금 더 닦아주세요";

    [Tooltip("단계를 잘 마쳤을 때. {zone} 사용 가능. 예: \"{zone} 깨끗해졌어요!\"")]
    [SerializeField] private string stepPraiseTemplate = "{zone} 깨끗해졌어요!";

    [Tooltip("루틴 전체를 잘 마쳤을 때.")]
    [SerializeField] private string finishSuccessText = "양치 완성! 정말 잘했어요!";

    [Tooltip("시간이 다 됐지만 일부 구역이 부족했을 때.")]
    [SerializeField] private string finishIncompleteText = "다 했어요! 다음엔 구석구석 더 닦아봐요";

    [Tooltip("한동안 양치를 멈췄을 때. {zone} 사용 가능.")]
    [SerializeField] private string idleTemplate = "양치를 하고 있지 않아요! {zone}를 닦아주세요";

    [Header("Behavior")]
    [Tooltip("같은 구역을 벗어났을 때 'OO 닦아주세요' 교정 멘트를 다시 띄우기까지의 최소 간격(초). " +
             "너무 잦은 잔소리를 막는다.")]
    [SerializeField, Min(0f)] private float correctCooldown = 4f;

    [Tooltip("양치를 멈춘 뒤 '양치를 하고 있지 않아요' 멘트를 띄우기까지 기다리는 시간(초). " +
             "잠깐 손 떼는 것엔 반응하지 않도록 약간의 유예를 둔다.")]
    [SerializeField, Min(0f)] private float idleThreshold = 2f;

    [Tooltip("켜면 발행되는 대사를 콘솔에도 찍는다 — 대사창 UI 구현 전 검증용.")]
    [SerializeField] private bool logEvents = true;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // 마지막 교정 멘트를 띄운 시각 — correctCooldown throttle용.
    private float lastCorrectTime = -999f;

    // 직전 Progress의 onTarget 상태 — 벗어나는 "전환 순간"에만 교정 멘트를 띄우기 위함.
    private bool wasOnTarget = true;

    // 양치를 멈춘 채 흐른 시간(초). idleThreshold를 넘으면 '양치 안 함' 멘트를 한 번 띄운다.
    private float idleElapsed;

    // 이번 idle 구간에서 이미 멘트를 띄웠는지 — 멈춰 있는 동안 반복 발화를 막는다.
    private bool idleSpoken;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus.Subscribe<RoutineStepStartedEvent>(OnStepStarted);
        EventBus.Subscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Subscribe<RoutineStepCompletedEvent>(OnStepCompleted);
        EventBus.Subscribe<RoutineCompletedEvent>(OnRoutineCompleted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RoutineStepStartedEvent>(OnStepStarted);
        EventBus.Unsubscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Unsubscribe<RoutineStepCompletedEvent>(OnStepCompleted);
        EventBus.Unsubscribe<RoutineCompletedEvent>(OnRoutineCompleted);
    }

    // ─────────────────────────────────────────────────────────────
    // 루틴 이벤트 핸들러 → 대사 선택
    // ─────────────────────────────────────────────────────────────

    /// <summary>새 단계 시작 → "이제 OO를 닦아볼까요?" 안내.</summary>
    private void OnStepStarted(RoutineStepStartedEvent e)
    {
        wasOnTarget = true;       // 새 단계 진입 직후엔 교정 멘트를 바로 띄우지 않는다.
        lastCorrectTime = Time.time;
        idleElapsed = 0f;         // 새 단계 안내가 곧 나오므로 idle 카운터도 리셋.
        idleSpoken = false;
        Speak(guideTemplate, e.targetZone, DialogueKind.Guide);
    }

    /// <summary>
    /// 진행 중 → 대상 구역을 벗어나 다른 곳을 닦기 시작한 "전환 순간"에만 교정 멘트.
    /// 매 프레임 띄우면 시끄러우므로 cooldown + 상태 전환 조건을 함께 건다.
    /// </summary>
    private void OnProgress(RoutineProgressEvent e)
    {
        bool isBrushing = e.currentBrushingZone.HasValue;

        if (!isBrushing)
        {
            // 양치를 멈춘 상태 — idle 시간을 누적하고, 임계치를 넘으면 한 번만 안내.
            idleElapsed += Time.deltaTime;
            if (idleElapsed >= idleThreshold && !idleSpoken)
            {
                idleSpoken = true;
                Speak(idleTemplate, e.targetZone, DialogueKind.Idle);
            }
            // 멈춰 있는 동안엔 구역 교정 로직을 돌릴 필요가 없다.
            wasOnTarget = true;   // 다시 닦기 시작하면 '벗어남' 전환으로 잡히지 않게.
            return;
        }

        // 다시 양치를 시작했으니 idle 카운터 리셋.
        idleElapsed = 0f;
        idleSpoken = false;

        // 대상을 닦다가(또는 대기) → 다른 구역을 닦기 시작한 순간에만 교정 멘트.
        bool startedWrongZone = !e.isOnTarget && e.currentBrushingZone.Value != e.targetZone;

        if (startedWrongZone && wasOnTarget && Time.time - lastCorrectTime >= correctCooldown)
        {
            lastCorrectTime = Time.time;
            Speak(correctTemplate, e.targetZone, DialogueKind.Correct);
        }

        wasOnTarget = e.isOnTarget;
    }

    /// <summary>단계 종료 → 목표 달성이면 칭찬, 미달이면 격려.</summary>
    private void OnStepCompleted(RoutineStepCompletedEvent e)
    {
        if (e.reachedTarget)
            Speak(stepPraiseTemplate, e.targetZone, DialogueKind.Praise);
        else
            Speak(encourageTemplate, e.targetZone, DialogueKind.Encourage);
    }

    /// <summary>루틴 전체 종료 → 성공/미완료 분기.</summary>
    private void OnRoutineCompleted(RoutineCompletedEvent e)
    {
        string text = e.allStepsReachedTarget ? finishSuccessText : finishIncompleteText;
        Publish(text, DialogueKind.Praise);
    }

    // ─────────────────────────────────────────────────────────────
    // 발행 헬퍼
    // ─────────────────────────────────────────────────────────────

    /// <summary>템플릿의 "{zone}"을 구역 한글명으로 치환해 발행.</summary>
    private void Speak(string template, UiZone zone, DialogueKind kind)
    {
        string text = template.Replace("{zone}", ZoneLabels.Korean(zone));
        if (logEvents) Debug.Log($"[Dialogue/{kind}] {text}");
        EventBus.Publish(new CharacterDialogueEvent { text = text, kind = kind, zone = zone, hasZone = true });
    }

    /// <summary>최종 문자열을 CharacterDialogueEvent로 발행.</summary>
    private void Publish(string text, DialogueKind kind)
    {
        if (logEvents) Debug.Log($"[Dialogue/{kind}] {text}");
        EventBus.Publish(new CharacterDialogueEvent { text = text, kind = kind });
    }
}
