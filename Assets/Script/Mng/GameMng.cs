using UnityEngine;

/// <summary>
/// 앱 전체의 거시 상태(부팅·타이틀·양치·결과)를 관리하는 상태 머신.
/// 상태 전환 시 GameStateChangedEvent를 EventBus로 발행하여
/// UI/씬 로더 등 관심 있는 시스템이 반응할 수 있게 한다.
/// </summary>
public class GameMng : Singleton<GameMng>
{
    protected GameMng() { }

    /// <summary>앱이 가질 수 있는 거시 상태.</summary>
    public enum GameState
    {
        Boot,      // 앱 시작 직후 — 매니저/리소스 초기화 단계
        Title,     // 타이틀/메인 메뉴 화면
        Brushing,  // 양치 진행 중 (MediaPipe + Sentis ML 가동)
        Result     // 양치 종료 후 결과/보상 화면
    }

    /// <summary>
    /// 상태 전환 이벤트 페이로드.
    /// struct로 정의하여 EventBus 발행 시 GC 부담을 최소화한다.
    /// </summary>
    public struct GameStateChangedEvent
    {
        // 전환 직전의 상태.
        public GameState Prev;
        // 전환 직후의 상태(=GameMng.Current).
        public GameState Next;
    }

    // 현재 상태. 외부에서는 읽기 전용이며 변경은 반드시 ChangeState를 거치도록 강제.
    // 초기값 Boot — 매니저 생성 시점에는 아직 어떤 화면도 표시되지 않은 상태.
    public GameState Current { get; private set; } = GameState.Boot;

    /// <summary>
    /// 상태를 next로 전환하고 GameStateChangedEvent를 발행한다.
    /// 같은 상태로의 재진입은 무시하여 중복 이벤트와 불필요한 씬 리로드를 방지.
    /// </summary>
    /// <param name="next">전환할 다음 상태.</param>
    public void ChangeState(GameState next)
    {
        // 동일 상태 재진입 차단 — Title→Title 같은 호출은 의도치 않은 부작용을 만든다.
        if (Current == next) return;

        // 발행에 쓸 이전 상태를 보관한 뒤 현재 상태를 갱신한다(순서 중요).
        var prev = Current;
        Current = next;

        // 상태 전환 흐름은 디버깅에서 핵심 단서이므로 항상 로그를 남긴다.
        Debug.Log($"[GameMng] 상태 전환: {prev} → {next}");

        // TODO: 상태별 씬 매핑이 정해지면 여기서 SceneManager.LoadSceneAsync 호출.
        //       예) Brushing 진입 시 "Brushing.unity" 로드, Title/Result는 "Main.unity" 유지.

        // 마지막에 이벤트 발행 — 구독자가 GameMng.Current를 읽으면 이미 새 상태가 반영되어 있어야 한다.
        EventBus.Publish(new GameStateChangedEvent { Prev = prev, Next = next });
    }
}
