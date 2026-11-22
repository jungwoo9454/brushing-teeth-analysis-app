/// <summary>
/// 다른 UI 위에 누적되어 표시되는 모달/팝업의 베이스.
/// UIMng의 popupStack에 쌓이며 ClosePopup 호출 시 최상단부터 파괴된다.
/// 스크린과 달리 인스턴스 보존 없이 Destroy되므로 일회성 사용에 적합.
/// </summary>
public abstract class UIPopup : UIBase
{
}
