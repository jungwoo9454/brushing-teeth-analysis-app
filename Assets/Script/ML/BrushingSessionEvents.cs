/// <summary>
/// 양치 세션 시작을 알리는 이벤트. BrushingSession.StartSession에서 발행.
/// UI는 이걸 받아 카운터 초기화, 타이머 화면 표시 등 시작 상태로 전환한다.
/// </summary>
public struct BrushingSessionStartedEvent
{
    /// <summary>권장 전체 양치 시간 (초). 일반 가이드 120초.</summary>
    public float targetTotal;

    /// <summary>zone당 권장 시간 (초). 5-zone 균등이면 targetTotal/5.</summary>
    public float targetPerZone;
}

/// <summary>
/// 양치 세션 진행 상황을 실시간으로 UI에 알리는 이벤트.
/// BrushingSession이 기본 0.1초마다 발행 — UI 업데이트 주기를 일정하게 유지한다.
/// zoneElapsed 배열은 호출자가 안전하게 읽을 수 있도록 BrushingSession이 매 발행 시 복사본을 만든다.
/// </summary>
public struct BrushingSessionTickEvent
{
    /// <summary>세션 시작 이후 전체 양치 누적 시간 (초). engaged 프레임만 누적.</summary>
    public float totalElapsed;

    /// <summary>
    /// 5-zone별 누적 시간 (초). 인덱스는 UiZone enum과 1:1
    /// (0=UpperLeft, 1=UpperRight, 2=LowerLeft, 3=LowerRight, 4=Front).
    /// SideLeft 검출은 UpperLeft+LowerLeft 둘 다 가산되어 들어온다.
    /// </summary>
    public float[] zoneElapsed;

    /// <summary>목표 전체 시간 (Started 이벤트와 동일).</summary>
    public float targetTotal;

    /// <summary>zone당 목표 시간 (Started 이벤트와 동일).</summary>
    public float targetPerZone;

    /// <summary>
    /// 이번 프레임의 stableZone (UI에서 "현재 어디를 닦는 중" 하이라이트에 사용).
    /// engaged=false면 None.
    /// </summary>
    public MlZone currentZone;

    /// <summary>이번 프레임에 칫솔이 입에 닿았는지 (UI에서 "양치 중" 인디케이터에 사용).</summary>
    public bool isEngaged;
}

/// <summary>
/// 양치 세션 종료를 알리는 이벤트. 자동(목표 도달)·수동(외부 EndSession) 모두 발행.
/// UI는 이걸 받아 결과 화면으로 전환하거나 통계를 표시한다.
/// </summary>
public struct BrushingSessionCompletedEvent
{
    /// <summary>최종 전체 양치 시간 (초).</summary>
    public float totalElapsed;

    /// <summary>5-zone별 최종 누적 시간 (초). UiZone enum index 기준.</summary>
    public float[] zoneElapsed;

    /// <summary>목표 전체 시간 (참고용).</summary>
    public float targetTotal;

    /// <summary>zone당 목표 시간 (참고용).</summary>
    public float targetPerZone;

    /// <summary>
    /// 모든 zone이 zone별 목표에 도달했고 전체 시간도 목표 이상인가.
    /// UI는 이걸로 "성공/미완료" 메시지를 분기.
    /// </summary>
    public bool achievedTarget;
}
