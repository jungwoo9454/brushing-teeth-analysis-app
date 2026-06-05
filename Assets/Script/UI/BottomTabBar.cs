using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 바텀 탭(Home/Report/Buddy/Setting) 바. "Bottom Tab" 프리팹 루트에 붙인다.
///
/// 역할:
///   1) 4개 탭 버튼 클릭 → 해당 탭 화면으로 전환(기본: AppRouter.ShowTab).
///   2) 활성 탭 시각화 — 활성 탭 버튼의 Image 스프라이트를 '활성화(파란)' 스프라이트로 교체하고,
///      나머지는 원래(일반) 스프라이트로 되돌린다.
///
/// 두 가지 화면 구조를 모두 지원한다:
///   A) 화면별 프리팹에 이 Bottom Tab을 포함시키는 구조(현재 프로젝트 방식):
///      각 화면 프리팹의 이 바에서 currentTab을 그 화면의 탭으로 지정한다.
///      탭을 누르면 AppRouter가 화면 프리팹을 통째로 교체하고, 새 화면의 바가 자기 탭을 활성 표시한다.
///   B) 바를 하나만 유지하고 콘텐츠만 바꾸는 구조:
///      navigateViaAppRouter를 끄고 onTabSelected로 콘텐츠를 갈아끼우면, 이 바는 파괴되지 않으므로
///      클릭 시 스스로 활성 표시를 갱신한다.
///
/// 인스펙터 작업을 최소화하기 위해, 버튼과 '일반' 스프라이트는 런타임에 자동으로 잡는다:
///   - 버튼: 자식 이름(Home/Report/Buddy/Setting)으로 자동 탐색(필드로 직접 지정도 가능).
///   - 일반 스프라이트: 각 버튼 Image의 현재 스프라이트를 Awake에서 캡처.
/// 따라서 인스펙터에서는 '활성화 스프라이트' 4개와 currentTab만 지정하면 된다.
/// (활성화 스프라이트는 Assets/Texture/Bottom Tab/ 의 *_활성화 = 파란 버전.)
/// </summary>
[DisallowMultipleComponent]
public class BottomTabBar : MonoBehaviour
{
    [Serializable]
    public class TabSelectedEvent : UnityEvent<BottomTab> { }

    [Header("이 바가 표시하는 현재 탭")]
    [SerializeField] private BottomTab currentTab = BottomTab.Home;

    [Header("활성화(파란) 스프라이트 — Texture/Bottom Tab/*_활성화")]
    [SerializeField] private Sprite homeActiveSprite;
    [SerializeField] private Sprite reportActiveSprite;
    [SerializeField] private Sprite buddyActiveSprite;
    [SerializeField] private Sprite settingActiveSprite;

    [Header("버튼 (비우면 자식 이름으로 자동 탐색)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button reportButton;
    [SerializeField] private Button buddyButton;
    [SerializeField] private Button settingButton;

    [Header("동작")]
    [Tooltip("켜면 탭 클릭 시 AppRouter.ShowTab으로 화면 프리팹을 전환한다. 끄면 onTabSelected만 호출하고 이 바가 직접 활성 표시를 갱신한다.")]
    [SerializeField] private bool navigateViaAppRouter = true;

    [Tooltip("탭이 선택될 때 호출 — AppRouter 전환과 별개로 추가 동작이 필요할 때 사용.")]
    [SerializeField] private TabSelectedEvent onTabSelected = new TabSelectedEvent();

    // 자동 탐색 키 — 프리팹 버튼 GameObject 이름과 일치해야 한다.
    private const string NameHome    = "Home";
    private const string NameReport  = "Report";
    private const string NameBuddy   = "Buddy";
    private const string NameSetting = "Setting";

    /// <summary>탭 한 칸의 런타임 정보.</summary>
    private class Tab
    {
        public BottomTab id;
        public Button button;
        public Image image;
        public Sprite normalSprite;   // 프리팹의 원래(일반) 아이콘
        public Sprite activeSprite;   // 활성화(파란) 아이콘
    }

    private readonly List<Tab> _tabs = new List<Tab>();

    public BottomTab CurrentTab => currentTab;

    private void Awake()
    {
        BuildTab(BottomTab.Home,    homeButton,    NameHome,    homeActiveSprite);
        BuildTab(BottomTab.Report,  reportButton,  NameReport,  reportActiveSprite);
        BuildTab(BottomTab.Buddy,   buddyButton,   NameBuddy,   buddyActiveSprite);
        BuildTab(BottomTab.Setting, settingButton, NameSetting, settingActiveSprite);

        ApplyVisual();
    }

    private void BuildTab(BottomTab id, Button button, string childName, Sprite activeSprite)
    {
        if (button == null)
        {
            var t = transform.Find(childName);
            if (t != null) button = t.GetComponent<Button>();
        }
        if (button == null)
        {
            Debug.LogWarning($"[BottomTabBar] '{childName}' 버튼을 찾지 못했다 — 자식 이름 또는 인스펙터 참조를 확인하라.");
            return;
        }

        var image = button.GetComponent<Image>();
        var tab = new Tab
        {
            id = id,
            button = button,
            image = image,
            normalSprite = image != null ? image.sprite : null,
            activeSprite = activeSprite,
        };
        _tabs.Add(tab);

        button.onClick.AddListener(() => Select(id));
    }

    /// <summary>탭 선택 — 기본은 AppRouter로 화면 전환. 현재 탭을 다시 누르면 무시한다.</summary>
    public void Select(BottomTab tab)
    {
        if (tab == currentTab) return;

        onTabSelected.Invoke(tab);

        if (navigateViaAppRouter)
        {
            var router = AppRouter.ins;
            if (router == null)
            {
                Debug.LogError("[BottomTabBar] AppRouter를 찾을 수 없다 — 탭 전환 불가.");
                return;
            }
            // 화면 프리팹이 통째로 교체되며, 새 화면의 BottomTabBar가 자기 탭을 활성 표시한다.
            router.ShowTab(tab);
        }
        else
        {
            // 바가 유지되는 구조 — 스스로 활성 표시 갱신.
            SetCurrent(tab);
        }
    }

    /// <summary>현재 탭을 지정하고 시각화를 갱신한다(외부에서 강제 동기화할 때도 사용).</summary>
    public void SetCurrent(BottomTab tab)
    {
        currentTab = tab;
        ApplyVisual();
    }

    // 활성 탭만 활성화 스프라이트, 나머지는 일반 스프라이트로 되돌린다.
    // (활성화 스프라이트가 아직 연결되지 않은 탭은 일반 스프라이트를 유지 — 기틀만 잡은 단계 대비)
    private void ApplyVisual()
    {
        foreach (var t in _tabs)
        {
            if (t.image == null) continue;
            bool active = t.id == currentTab;
            var sprite = active ? t.activeSprite : t.normalSprite;
            if (sprite != null) t.image.sprite = sprite;
        }
    }
}
