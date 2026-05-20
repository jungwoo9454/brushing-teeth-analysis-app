/// <summary>
/// 전체 화면을 차지하는 UI(타이틀·양치·결과 등)의 베이스.
/// UIMng의 screenStack에서 한 번에 하나만 활성 상태가 되며,
/// 새 스크린이 Push되면 이전 스크린은 비활성화된 채 스택에 보존되어 Back으로 복귀 가능.
/// </summary>
public abstract class UIScreen : UIBase
{
}
