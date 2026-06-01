using UnityEngine;

/// <summary>
/// 양치 흐름 이벤트에 맞춰 캐릭터 모션을 전환하는 프리젠터.
/// 공용 스테이지 렌더링 / 애니 재생 / idle 표정 변주는 베이스 <see cref="CharacterStage"/>가 담당하고,
/// 이 클래스는 "어떤 상황에 어떤 모션을 낼지"(양치 시작·종료·대사·양치여부)만 더한다.
///   - 세션 시작        → Hello (인사)
///   - 칭찬 대사(Praise) → 긍정 모션(웃음/엄지척 등)
///   - 부족/교정/미양치 대사 → 부정 모션(슬픔/도리도리 등). 단 '양치를 안 하고 있을 때만'.
///   - 세션 종료(성공)   → Win, (미흡) → DontKnow
///
/// 스테이지/캐릭터/조명/idle 변주 설정은 베이스의 인스펙터 필드에서 조정한다.
/// Brushing 씬의 RawImage("Chracter")가 있는 오브젝트에 부착한다.
/// </summary>
public class CharacterStagePresenter : CharacterStage
{
    [Header("양치 상황별 애니메이션 State (Animator Controller의 state명)")]
    [Tooltip("세션 시작 인사.")]
    [SerializeField] private string startState = "Hello";
    [Tooltip("긍정 대화(칭찬 등) — 이 중 랜덤. 정면을 보는 긍정 표정만.")]
    [SerializeField] private string[] goodStates = { "ThumbsUp", "Laught", "Win" };
    [Tooltip("부정 대화(미양치/부족/교정) — 이 중 랜덤. '양치를 안 하고 있을 때만' 나온다.")]
    [SerializeField] private string[] badStates = { "No", "Cry", "Emo Angry" };
    [Tooltip("세션 성공 종료.")]
    [SerializeField] private string finishSuccessState = "Win";
    [Tooltip("세션 미흡 종료.")]
    [SerializeField] private string finishIncompleteState = "DontKnow";

    [Header("양치 동작 타이밍")]
    [Tooltip("긍정/부정 표정을 다시 트리거하기까지 최소 간격(초). 잦은 전환 방지.")]
    [SerializeField, Min(0f)] private float moodCooldown = 3f;
    [Tooltip("시작 인사 모션을 보여준 뒤 idle로 복귀하기까지의 시간(초).")]
    [SerializeField, Min(0.1f)] private float startHold = 2.5f;
    [Tooltip("긍정/부정 표정을 보여준 뒤 idle로 복귀하기까지의 시간(초).")]
    [SerializeField, Min(0.1f)] private float moodHold = 3f;

    // 내부 상태 — mood 게이팅.
    private float _lastMoodTime = -999f;
    private bool _lastGood;   // 마지막으로 보여준 mood가 긍정이었는지.
    private bool _hasMood;
    private bool _isEngaged;  // 가장 최근 ZoneDetectedEvent의 engaged — 부정 표정 게이트.

    private void OnEnable()
    {
        EventBus.Subscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Subscribe<CharacterDialogueEvent>(OnDialogue);
        EventBus.Subscribe<ZoneDetectedEvent>(OnZoneDetected);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZoneDetected);
        EventBus.Unsubscribe<CharacterDialogueEvent>(OnDialogue);
    }

    private void OnSessionStarted(BrushingSessionStartedEvent e)
    {
        _hasMood = false;
        // 인사 모션을 잠깐 보여준 뒤 idle로 자동 복귀.
        PlayTransient(startState, startHold);
    }

    private void OnSessionCompleted(BrushingSessionCompletedEvent e)
    {
        // 종료 연출은 idle로 복귀하지 않고 그대로 유지.
        PlayPersistent(e.achievedTarget ? finishSuccessState : finishIncompleteState);
    }

    /// <summary>가장 최근 양치 여부(engaged) 추적 — 부정 표정 게이트에 사용.</summary>
    private void OnZoneDetected(ZoneDetectedEvent e) => _isEngaged = e.isEngaged;

    /// <summary>
    /// 대사 성격(말풍선)에 맞춰 캐릭터 모션을 일치시킨다.
    ///   Praise(칭찬) → 긍정 / Encourage·Correct·Idle(부족·교정·미양치) → 부정(단 !engaged일 때만).
    ///   Guide(단순 안내) → mood 변화 없음.
    /// </summary>
    private void OnDialogue(CharacterDialogueEvent e)
    {
        switch (e.kind)
        {
            case DialogueKind.Praise:
                SetMood(true);
                break;
            case DialogueKind.Encourage:
            case DialogueKind.Correct:
            case DialogueKind.Idle:
                if (!_isEngaged) SetMood(false); // 양치 중이면 부정 표정을 내지 않는다.
                break;
            // Guide 등은 무시.
        }
    }

    /// <summary>긍정/부정 표정 전환 — 쿨다운 + 같은 mood 연속이면 스킵. 풀에서 랜덤 재생 후 idle 복귀.</summary>
    private void SetMood(bool good)
    {
        if (_hasMood && _lastGood == good) return;
        if (Time.unscaledTime - _lastMoodTime < moodCooldown) return;

        _lastMoodTime = Time.unscaledTime;
        _lastGood = good;
        _hasMood = true;

        PlayTransient(PickRandom(good ? goodStates : badStates), moodHold);
    }
}
