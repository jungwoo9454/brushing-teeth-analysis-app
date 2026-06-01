using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙여 AppRouter의 화면 전환 동작 하나를 연결하는 헬퍼.
/// 같은 GameObject의 Button.onClick에 자동으로 등록되므로, 인스펙터에서 UnityEvent를
/// 수동으로 연결하지 않아도 된다(프리팹·씬 어디에 있든 런타임에 AppRouter.ins를 찾아 호출).
///
/// 사용: 버튼 오브젝트에 이 컴포넌트를 추가하고 action만 고르면 끝.
///   - GoBrushing  : 양치 씬으로 이동 (메뉴의 '양치 시작' 이미지 버튼)
///   - GoMainMenu  : 메인 씬으로 이동 + 메뉴 표시 (양치 씬 Close 버튼)
///   - GoMainFinish: 메인 씬으로 이동 + 결과 화면 표시
/// </summary>
[RequireComponent(typeof(Button))]
public class AppNavButton : MonoBehaviour
{
    public enum NavAction
    {
        GoBrushing,
        GoMainMenu,
        GoMainFinish,
    }

    [Tooltip("이 버튼을 눌렀을 때 수행할 화면 전환.")]
    [SerializeField] private NavAction action = NavAction.GoBrushing;

    private void Awake()
    {
        var button = GetComponent<Button>();
        button.onClick.AddListener(Invoke);
    }

    private void Invoke()
    {
        var router = AppRouter.ins;
        if (router == null)
        {
            Debug.LogError("[AppNavButton] AppRouter를 찾을 수 없다.");
            return;
        }

        switch (action)
        {
            case NavAction.GoBrushing:   router.GoBrushing();   break;
            case NavAction.GoMainMenu:   router.GoMainMenu();   break;
            case NavAction.GoMainFinish: router.GoMainFinish(); break;
        }
    }
}
