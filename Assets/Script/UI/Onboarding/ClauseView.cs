using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 약관 동의 화면(Clause). Clause 프리팹 루트(Canvas)에 부착한다.
///
/// 동작:
///   - Scroll View/Viewport/Content/clauses 아래의 각 'clause' 항목에는 'Check' 이미지가 있다.
///     'clause'를 클릭하면 체크가 토글되고, Check 이미지가 No Check ↔ Check 스프라이트로 바뀐다.
///   - 'All Agree'를 클릭하면 아래 모든 항목이 한 번에 체크/해제된다(전체 동의).
///   - 라벨에 '[필수]'가 있는 항목이 모두 체크되면 'Continue agree' 버튼이 활성화 → ParentInfo로 이동.
///     ([필수] 표기가 하나도 없으면 모든 항목을 필수로 간주 — 약관이 1개면 그것만 체크해도 진행 가능.)
///
/// Check/No Check 스프라이트는 인스펙터에 연결한다(Assets/Texture/InitMenu/Check.png, Nocheck.png).
/// noCheckSprite를 비우면 시작 시 Check 이미지의 현재(기본) 스프라이트를 No Check로 캡처한다.
/// </summary>
[DisallowMultipleComponent]
public class ClauseView : MonoBehaviour
{
    [Header("체크 스프라이트 (Assets/Texture/InitMenu/)")]
    [Tooltip("체크됐을 때 표시할 스프라이트 — Check.png")]
    [SerializeField] private Sprite checkSprite;
    [Tooltip("체크 안 됐을 때 스프라이트 — Nocheck.png. 비우면 시작 시 Check 이미지의 현재 스프라이트로 캡처.")]
    [SerializeField] private Sprite noCheckSprite;

    private const string PathClauses  = "RealSpace/Scroll View/Viewport/Content/clauses";
    private const string PathAllAgree = "RealSpace/All Agree";
    private const string PathContinue = "RealSpace/Continue agree";
    private const string PathBack     = "RealSpace/Top/Back";

    private class Item { public Image check; public bool required; public bool on; }

    private readonly List<Item> _items = new List<Item>();
    private Image _allCheck;
    private Button _continue;

    private void Awake()
    {
        _continue = transform.Find(PathContinue)?.GetComponent<Button>();

        // 전체 동의 — All Agree 클릭 시 모든 항목 토글, IsCheck 이미지로 상태 표시.
        var allAgree = transform.Find(PathAllAgree);
        if (allAgree != null)
        {
            _allCheck = allAgree.Find("IsCheck")?.GetComponent<Image>();
            // IsCheck에 붙은 InputField가 클릭을 가로채지 않도록 레이캐스트를 끈다 → 클릭이 All Agree 버튼으로 전달.
            if (_allCheck != null) _allCheck.raycastTarget = false;
            var allBtn = OnboardingUtil.EnsureButton(allAgree);
            if (allBtn != null) allBtn.onClick.AddListener(OnAllAgree);
        }

        // 개별 약관 항목 수집.
        var clauses = transform.Find(PathClauses);
        if (clauses != null)
        {
            foreach (Transform c in clauses)
            {
                var check = c.Find("Check")?.GetComponent<Image>();
                if (check == null) continue;
                if (noCheckSprite == null) noCheckSprite = check.sprite;   // 기본(No Check) 캡처.

                string label = c.Find("Text (TMP)")?.GetComponent<TMP_Text>()?.text ?? "";
                var item = new Item { check = check, required = label.Contains("[필수]"), on = false };
                _items.Add(item);

                var btn = OnboardingUtil.EnsureButton(c);
                if (btn != null) btn.onClick.AddListener(() => { item.on = !item.on; Refresh(); });
            }
        }

        // [필수] 표기가 하나도 없으면 모든 항목을 필수로 간주.
        bool anyRequired = false;
        foreach (var it in _items) if (it.required) { anyRequired = true; break; }
        if (!anyRequired) foreach (var it in _items) it.required = true;

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.InitMenu));

        if (_continue != null) _continue.onClick.AddListener(OnContinue);

        Refresh();
    }

    private void OnAllAgree()
    {
        // 모두 체크돼 있으면 해제, 아니면 전체 체크.
        bool allOn = AllChecked();
        foreach (var it in _items) it.on = !allOn;
        Refresh();
    }

    private void OnContinue()
    {
        if (!RequiredAllChecked()) return;
        UserMng.ins.SetTermsAgreed(true);
        AppRouter.ins.ShowScreen(AppRouter.Screen.ParentInfo);
    }

    private bool AllChecked()
    {
        if (_items.Count == 0) return false;
        foreach (var it in _items) if (!it.on) return false;
        return true;
    }

    private bool RequiredAllChecked()
    {
        if (_items.Count == 0) return false;
        foreach (var it in _items) if (it.required && !it.on) return false;
        return true;
    }

    private void Refresh()
    {
        foreach (var it in _items)
            if (it.check != null) it.check.sprite = it.on ? checkSprite : noCheckSprite;

        if (_allCheck != null)
            _allCheck.sprite = AllChecked() ? checkSprite : noCheckSprite;

        if (_continue != null) _continue.interactable = RequiredAllChecked();
    }
}
