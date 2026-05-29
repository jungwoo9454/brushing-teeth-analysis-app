/// <summary>
/// 순차 양치 가이드(BrushingRoutineRunner)가 EventBus로 발행하는 이벤트 모음.
/// UI(하단 대사창·진행바)와 대사 디렉터(DialogueDirector)가 이걸 구독해 반응한다.
/// 모두 struct — GC 부담 없이 발행한다.
/// </summary>

/// <summary>루틴 전체 시작. 씬 진입 시 1회 발행되며 카운터/타이머를 0에서 시작한다.</summary>
public struct RoutineStartedEvent
{
    /// <summary>전체 단계 수.</summary>
    public int stepCount;

    /// <summary>전체 목표 시간(초). 기본 180초.</summary>
    public float totalSeconds;
}

/// <summary>
/// 한 단계(구역) 시작. 단계가 바뀔 때마다 발행 — UI는 "이제 OO를 닦으세요" 안내로 전환한다.
/// </summary>
public struct RoutineStepStartedEvent
{
    /// <summary>현재 단계 인덱스(0-based).</summary>
    public int stepIndex;

    /// <summary>전체 단계 수.</summary>
    public int stepCount;

    /// <summary>이 단계에서 닦아야 할 구역.</summary>
    public UiZone targetZone;

    /// <summary>이 단계에 배분된 목표 시간(초).</summary>
    public float stepTargetSeconds;

    /// <summary>다음 단계가 있는가 (마지막 단계면 false → UI는 "다음 구역" 영역을 숨김).</summary>
    public bool hasNext;

    /// <summary>다음 단계의 구역 (hasNext=false면 무의미). UI의 "다음 구역" 표시에 사용.</summary>
    public UiZone nextZone;
}

/// <summary>
/// 단계 진행 상황. Runner가 일정 주기(기본 0.1초)로 발행 — UI 진행바/타이머 갱신용.
/// </summary>
public struct RoutineProgressEvent
{
    /// <summary>현재 단계 인덱스(0-based).</summary>
    public int stepIndex;

    /// <summary>현재 단계 대상 구역.</summary>
    public UiZone targetZone;

    /// <summary>현재 단계에서 대상 구역을 실제로 닦은 누적 시간(초).</summary>
    public float stepElapsed;

    /// <summary>현재 단계 목표 시간(초).</summary>
    public float stepTargetSeconds;

    /// <summary>세션 전체 경과 시간(초, 단계 무관하게 흐른 시간).</summary>
    public float totalElapsed;

    /// <summary>전체 목표 시간(초).</summary>
    public float totalSeconds;

    /// <summary>이번 프레임에 사용자가 실제로 닦고 있는 구역(미검출/대기면 null).</summary>
    public UiZone? currentBrushingZone;

    /// <summary>현재 닦는 구역이 이 단계의 목표 구역과 일치하는가 (불일치 시 "다른 곳 닦는 중" 안내).</summary>
    public bool isOnTarget;
}

/// <summary>
/// 한 단계 종료. 다음 단계로 넘어가기 직전 발행.
/// reachedTarget=false면 시간이 부족한 채 넘어간 것 → "조금 더 닦아주세요" 류 멘트 트리거.
/// </summary>
public struct RoutineStepCompletedEvent
{
    /// <summary>종료된 단계 인덱스(0-based).</summary>
    public int stepIndex;

    /// <summary>종료된 단계의 대상 구역.</summary>
    public UiZone targetZone;

    /// <summary>그 구역을 실제 닦은 누적 시간(초).</summary>
    public float stepElapsed;

    /// <summary>그 단계 목표 시간(초).</summary>
    public float stepTargetSeconds;

    /// <summary>목표 시간 이상 닦았는가.</summary>
    public bool reachedTarget;
}

/// <summary>
/// 루틴 전체 종료(모든 단계 완료 또는 전체 시간 소진). UI는 결과 화면 전환에 사용.
/// </summary>
public struct RoutineCompletedEvent
{
    /// <summary>세션 전체 경과 시간(초).</summary>
    public float totalElapsed;

    /// <summary>전체 목표 시간(초).</summary>
    public float totalSeconds;

    /// <summary>모든 단계가 각자 목표 시간을 채웠는가.</summary>
    public bool allStepsReachedTarget;
}
