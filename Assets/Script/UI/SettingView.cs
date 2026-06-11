using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정(Setting) 화면 바인딩. Setting.prefab 루트(Canvas)에 부착한다.
///
/// 표시/동작:
///   1) 프로필 — 보호자 이름 + 호칭(엄마/아빠/조부모/선생님)을 "이름 호칭"으로. 이메일 줄은 그대로 보존.
///   2) 아이 목록 — Childs 아래 Child 항목을 등록된 아이 수만큼 복제해 각 아이(이름·나이)를 표시.
///      · 아이 항목 클릭 → 활성 아이 전환(SetActiveChild) → 리포트/버디가 그 아이 데이터로 바뀜.
///      · 활성 아이 항목에만 'Current' 텍스트를 켜서 지금 어떤 아이가 활성인지 보여준다.
///   3) Add Child — 목록 맨 아래 버튼 → AddChild 창을 팝업으로 띄움. 추가 완료 시 목록 갱신.
///
/// 자식 참조는 경로로 자동 탐색하므로 인스펙터에는 addChildPrefab(AddChild)만 지정하면 된다.
/// </summary>
[DisallowMultipleComponent]
public class SettingView : MonoBehaviour
{
    [Header("아이 추가 팝업")]
    [Tooltip("설정에서 '아이 추가'로 띄울 AddChild 프리팹.")]
    [SerializeField] private GameObject addChildPrefab;

    private const string PathProfile   = "RealSpace/Profile";
    private const string PathChilds    = "RealSpace/Scroll View/Viewport/Content/Childs";
    private const string PathTemplate  = PathChilds + "/Child";
    private const string PathAddChild  = PathChilds + "/Add Child";
    private const string PathBack      = "RealSpace/Top/Back";
    private const string ChildNamePath = "Text (TMP)";
    private const string ChildCurrent  = "Current";

    private Transform _childs;
    private Transform _template;
    private Transform _addChild;
    private TMP_Text _profileText;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject _addChildPopup;

    private void Awake()
    {
        _childs      = transform.Find(PathChilds);
        _template    = transform.Find(PathTemplate);
        _addChild    = transform.Find(PathAddChild);
        _profileText = ResolveProfileText();

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.MainMenu));

        var addBtn = OnboardingUtil.EnsureButton(_addChild);
        if (addBtn != null) addBtn.onClick.AddListener(OpenAddChild);

        // Child는 복제용 프로토타입 — 숨겨두고 아이 수만큼 복제한다.
        if (_template != null) _template.gameObject.SetActive(false);
    }

    private void OnEnable() => Refresh();

    // 설정이 닫히거나(탭 전환) 파괴되면 떠 있던 추가 팝업도 정리.
    private void OnDisable() => CloseAddChild();
    private void OnDestroy() => CloseAddChild();

    /// <summary>프로필 + 아이 목록을 현재 데이터로 다시 그린다.</summary>
    public void Refresh()
    {
        BindProfile();
        BuildChildList();
    }

    // ── 프로필 ────────────────────────────────────────────────────

    private void BindProfile()
    {
        if (_profileText == null) return;

        var p = UserMng.ins.Profile;
        string name   = (p.guardianName ?? "").Trim();
        string prefix = (p.guardianPrefix ?? "").Trim();
        string display = string.IsNullOrEmpty(prefix) ? name : $"{name} {prefix}";
        if (string.IsNullOrEmpty(display)) return;   // 보호자 미설정이면 기존 표시 유지.

        // 기존 텍스트의 둘째 줄 이하(이메일)는 보존하고 첫 줄(이름)만 교체.
        string existing = _profileText.text ?? "";
        int nl = existing.IndexOf('\n');
        string tail = nl >= 0 ? existing.Substring(nl) : "";
        _profileText.text = display + tail;
    }

    // 프로필 텍스트 = Profile 아래 TMP 중 이메일('@')을 포함한 것(없으면 첫 TMP).
    private TMP_Text ResolveProfileText()
    {
        var profile = transform.Find(PathProfile);
        if (profile == null) return null;

        TMP_Text fallback = null;
        foreach (Transform c in profile)
        {
            var t = c.GetComponent<TMP_Text>();
            if (t == null) continue;
            if (fallback == null) fallback = t;
            if (!string.IsNullOrEmpty(t.text) && t.text.Contains("@")) return t;
        }
        return fallback;
    }

    // ── 아이 목록 ─────────────────────────────────────────────────

    private void BuildChildList()
    {
        if (_childs == null || _template == null) return;

        // 이전 복제본 제거.
        foreach (var go in _spawned) if (go != null) Destroy(go);
        _spawned.Clear();

        var children = UserMng.ins.Children;
        var active = UserMng.ins.ActiveChild;
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var go = Instantiate(_template.gameObject, _childs);
                go.SetActive(true);
                go.name = "Child_" + child.id;
                _spawned.Add(go);

                var nameText = go.transform.Find(ChildNamePath)?.GetComponent<TMP_Text>();
                if (nameText != null)
                    nameText.text = child.age > 0 ? $"{child.name}({child.age}세)" : child.name;

                var current = go.transform.Find(ChildCurrent);
                if (current != null)
                    current.gameObject.SetActive(active != null && child.id == active.id);

                string id = child.id;   // 클로저 캡처용 지역 복사.
                var btn = OnboardingUtil.EnsureButton(go.transform);
                if (btn != null) btn.onClick.AddListener(() => OnSelectChild(id));
            }
        }

        // Add Child 버튼은 항상 목록 맨 아래.
        if (_addChild != null) _addChild.SetAsLastSibling();
    }

    private void OnSelectChild(string childId)
    {
        UserMng.ins.SetActiveChild(childId);
        BuildChildList();   // Current 표시 갱신.
    }

    // ── 아이 추가 팝업 ────────────────────────────────────────────

    private void OpenAddChild()
    {
        if (_addChildPopup != null) return;   // 이미 열림.
        if (addChildPrefab == null)
        {
            Debug.LogError("[SettingView] addChildPrefab이 연결되지 않았다 — 인스펙터에서 AddChild 프리팹을 지정하라.");
            return;
        }

        _addChildPopup = Instantiate(addChildPrefab);
        var canvas = _addChildPopup.GetComponent<Canvas>();
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = 100; }

        var view = _addChildPopup.GetComponentInChildren<AddChildView>(true);
        if (view != null)
        {
            view.ConfigureForSettings(onAdded: OnChildAdded, onClose: CloseAddChild);
        }
        else
        {
            Debug.LogError("[SettingView] AddChild 프리팹에서 AddChildView를 찾지 못했다.");
            CloseAddChild();
        }
    }

    private void OnChildAdded()
    {
        CloseAddChild();
        BuildChildList();
    }

    private void CloseAddChild()
    {
        if (_addChildPopup == null) return;
        Destroy(_addChildPopup);
        _addChildPopup = null;
    }
}
