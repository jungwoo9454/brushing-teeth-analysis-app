using UnityEngine;

/// <summary>
/// 모든 UI 컴포넌트(스크린·팝업)의 공통 베이스.
/// 생명주기는 OnShow / OnHide 두 가지만 노출하여 단순함을 유지한다.
/// 인스턴스화·파괴는 UIMng가 일관되게 담당하며, 사용자 코드는 OnShow/OnHide만 오버라이드한다.
/// </summary>
public abstract class UIBase : MonoBehaviour
{
    /// <summary>
    /// 화면에 표시되기 시작할 때 호출.
    /// 애니메이션 시작·이벤트 구독·데이터 바인딩을 여기서 처리한다.
    /// </summary>
    public virtual void OnShow() { }

    /// <summary>
    /// 화면에서 가려지거나 파괴되기 직전에 호출.
    /// 이벤트 구독 해제·코루틴 정지·임시 리소스 반환을 여기서 처리한다.
    /// </summary>
    public virtual void OnHide() { }
}
