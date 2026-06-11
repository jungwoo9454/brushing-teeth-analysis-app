using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 온보딩 시작 화면(InitMenu). 'Create Account' 버튼 → 약관 동의(Clause) 화면으로 이동.
/// InitMenu 프리팹 루트(Canvas)에 부착한다.
/// </summary>
[DisallowMultipleComponent]
public class InitMenuView : MonoBehaviour
{
    private const string PathCreate = "RealSpace/Create Account";

    private void Awake()
    {
        var btn = OnboardingUtil.EnsureButton(transform.Find(PathCreate));
        if (btn != null)
            btn.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.Clause));
        else
            Debug.LogWarning("[InitMenuView] 'Create Account' 오브젝트를 찾지 못했다 — 경로 확인.");
    }
}
