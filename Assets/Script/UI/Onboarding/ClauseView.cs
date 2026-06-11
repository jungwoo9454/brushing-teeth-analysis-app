using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 약관 동의 화면(Clause). Clause 프리팹 루트(Canvas)에 부착한다.
///
/// 동작:
///   - clauses 아래 각 'clause' 칸 = Check(체크박스) + Image(상세 보기 버튼) + Text(라벨).
///     칸(Check/라벨)을 클릭하면 체크 토글(Check 이미지가 No Check ↔ Check 스프라이트로 바뀜).
///     칸의 'Image' 버튼을 누르면 'Clause Detail' 팝업이 켜지고 상세 내용(detail)이 표시된다.
///   - 'All Agree' 클릭 → 모든 항목 일괄 토글.
///   - 필수 항목이 모두 체크되면 'Continue agree' 활성 → ParentInfo로 이동.
///
/// 약관 목록(<see cref="clauses"/>)이 항목의 단일 출처다. 라벨/필수여부/상세내용을 여기서 정의하면
/// 프리팹의 clause 칸에 순서대로 채워지고, 칸 수가 모자라면 첫 칸을 템플릿으로 복제해 맞춘다.
/// </summary>
[DisallowMultipleComponent]
public class ClauseView : MonoBehaviour
{
    /// <summary>약관 항목 1개 — 목록 라벨, 필수 여부, 상세 내용(팝업).</summary>
    [System.Serializable]
    public class ClauseDef
    {
        [Tooltip("목록에 보이는 라벨(리치텍스트 가능).")]
        [TextArea(1, 2)] public string label;
        [Tooltip("필수 동의 항목인지(필수가 모두 체크돼야 Continue 활성).")]
        public bool required = true;
        [Tooltip("Clause Detail 팝업에 표시할 상세 내용.")]
        [TextArea(5, 20)] public string detail;
    }

    [Header("체크 스프라이트 (Assets/Texture/InitMenu/)")]
    [SerializeField] private Sprite checkSprite;     // Check.png
    [SerializeField] private Sprite noCheckSprite;   // Nocheck.png (비우면 시작 시 Check 이미지의 현재 스프라이트로 캡처)

    [Header("약관 항목 (순서대로 clause 칸에 채워진다)")]
    [SerializeField]
    private ClauseDef[] clauses =
    {
        new ClauseDef
        {
            label = "<b><color=#3B7FE8>[필수]</color></b> 만 14세 이상 및 보호자 동의",
            required = true,
            detail =
@"[만 14세 이상 및 보호자 동의]

1. 본 앱의 계정은 만 14세 이상인 보호자(부모·조부모·교사 등)가 생성하고 관리합니다.
2. 아동(만 14세 미만)은 보호자의 지도·감독 아래 본 앱을 이용합니다.
3. 보호자는 아동의 본 앱 이용에 동의하며, 이용 과정에서 발생하는 사항을 관리할 책임이 있습니다.
4. 본 항목에 동의하지 않으면 서비스 가입이 제한됩니다."
        },
        new ClauseDef
        {
            label = "<b><color=#3B7FE8>[필수]</color></b> 서비스 이용약관 동의",
            required = true,
            detail =
@"[서비스 이용약관]

제1조 (목적)
본 약관은 '세살버릇'(이하 ""앱"")이 제공하는 양치 코칭 서비스의 이용 조건과 절차를 정합니다.

제2조 (서비스 내용)
앱은 카메라와 온디바이스 AI로 양치 동작을 분석하고, 올바른 양치 습관 형성을 돕는 피드백·기록·리포트를 제공합니다.

제3조 (이용자의 의무)
1. 이용자는 앱을 본래 목적에 맞게 사용합니다.
2. 카메라는 양치 동작 분석에만 사용되며, 이용자는 적절한 환경에서 앱을 사용할 책임이 있습니다.

제4조 (데이터)
1. 양치 기록 등 모든 데이터는 이용자의 기기 안에만 저장되며, 앱은 별도의 서버로 데이터를 전송하지 않습니다.
2. 앱을 삭제하면 기기에 저장된 데이터도 함께 삭제될 수 있으니 주의하시기 바랍니다.

제5조 (책임의 한계)
앱이 제공하는 분석·피드백은 양치 습관 형성을 돕기 위한 참고용이며, 치과 진료나 의학적 진단을 대체하지 않습니다.

제6조 (약관의 변경)
약관은 관련 법령에 따라 변경될 수 있으며, 변경 시 앱 내 공지로 안내합니다."
        },
        new ClauseDef
        {
            label = "<b><color=#3B7FE8>[필수]</color></b> 개인정보 처리방침 동의",
            required = true,
            detail =
@"[개인정보 처리방침]

세살버릇은 이용자의 개인정보를 소중히 다루며, 다음과 같이 처리합니다.

1. 수집하는 개인정보
현재 본 앱은 회원가입·이용을 위해 별도의 개인정보를 서버로 수집하지 않습니다. 보호자·아이 정보(이름, 호칭, 나이, 성별), PIN, 양치 기록은 모두 이용자의 기기(로컬)에만 저장됩니다.

2. 카메라 영상의 처리
양치 분석을 위한 카메라 영상은 기기 안에서 실시간으로 처리되며, 저장하거나 외부로 전송하지 않습니다.

3. 보관·파기
모든 데이터는 이용자 기기에 저장되며, 앱 삭제 또는 데이터 초기화 시 파기됩니다.

4. 제3자 제공
앱은 이용자의 데이터를 제3자에게 제공하거나 외부로 전송하지 않습니다.

5. 이용자의 권리
이용자는 언제든지 기기 안의 데이터를 초기화·삭제할 수 있습니다.

※ 향후 서버 연동 등으로 처리 방침이 바뀔 경우, 앱 내 공지와 재동의 절차로 안내합니다."
        },
        new ClauseDef
        {
            label = "<b><color=#8B8F94>[선택]</color></b> 알림 수신 동의",
            required = false,
            detail =
@"[알림 수신 동의 (선택)]

1. 양치 시간 알림, 연속 기록 축하 등 서비스 이용에 도움이 되는 알림을 기기로 보냅니다.
2. 이 알림은 기기 안에서 생성되는 로컬 알림이며, 별도의 개인정보 수집을 수반하지 않습니다.
3. 선택 항목이므로 동의하지 않아도 서비스를 정상적으로 이용할 수 있습니다.
4. 알림 수신 여부는 기기 설정 또는 앱 설정에서 언제든지 바꿀 수 있습니다."
        },
    };

    private const string PathClauses  = "RealSpace/Scroll View/Viewport/Content/clauses";
    private const string PathAllAgree = "RealSpace/All Agree";
    private const string PathContinue = "RealSpace/Continue agree";
    private const string PathBack     = "RealSpace/Top/Back";
    private const string PathDetail   = "RealSpace/Clause Detail";
    private const string PathDetailText = "RealSpace/Clause Detail/Scroll View/Viewport/Content/Text (TMP)";
    private const string PathDetailClose = "RealSpace/Clause Detail/Close Text";

    private const string ChildCheck = "Check";
    private const string ChildImage = "Image";       // 약관별 '상세 보기' 버튼
    private const string ChildLabel = "Text (TMP)";

    private class Item { public Image check; public bool required; public bool on; }

    private readonly List<Item> _items = new List<Item>();
    private Image _allCheck;
    private Button _continue;
    private GameObject _detailPanel;
    private TMP_Text _detailText;

    private void Awake()
    {
        _continue = transform.Find(PathContinue)?.GetComponent<Button>();

        // 상세 팝업.
        _detailPanel = transform.Find(PathDetail)?.gameObject;
        _detailText  = transform.Find(PathDetailText)?.GetComponent<TMP_Text>();
        var close = OnboardingUtil.EnsureButton(transform.Find(PathDetailClose));
        if (close != null) close.onClick.AddListener(CloseDetail);
        if (_detailPanel != null) _detailPanel.SetActive(false);

        // 전체 동의.
        var allAgree = transform.Find(PathAllAgree);
        if (allAgree != null)
        {
            _allCheck = allAgree.Find("IsCheck")?.GetComponent<Image>();
            if (_allCheck != null) _allCheck.raycastTarget = false;   // IsCheck의 Toggle이 클릭을 가로채지 않게.
            var allBtn = OnboardingUtil.EnsureButton(allAgree);
            if (allBtn != null) allBtn.onClick.AddListener(OnAllAgree);
        }

        BuildItems();

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.InitMenu));

        if (_continue != null) _continue.onClick.AddListener(OnContinue);

        Refresh();
    }

    /// <summary>
    /// clauses 칸을 약관 목록 수에 맞춰 구성한다 — 기존 칸을 채우고, 모자라면 첫 칸을 복제하고, 남으면 비활성화.
    /// 각 칸: 라벨 채움 + Check 토글(칸 클릭) + Image '상세 보기' 버튼.
    /// </summary>
    private void BuildItems()
    {
        var parent = transform.Find(PathClauses);
        if (parent == null || clauses == null || clauses.Length == 0) return;

        // 기존 clause 칸(자식 중 Check가 있는 것) 수집.
        var slots = new List<Transform>();
        foreach (Transform c in parent)
            if (c.Find(ChildCheck) != null) slots.Add(c);
        if (slots.Count == 0) return;   // 템플릿이 없으면 구성 불가.

        Transform template = slots[0];

        // 약관 수만큼 칸을 확보(모자라면 템플릿 복제).
        while (slots.Count < clauses.Length)
        {
            var clone = Instantiate(template.gameObject, parent);
            clone.name = "clause (auto)";
            slots.Add(clone.transform);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (i >= clauses.Length) { slot.gameObject.SetActive(false); continue; }   // 남는 칸은 숨김.
            slot.gameObject.SetActive(true);

            var def = clauses[i];
            var check = slot.Find(ChildCheck)?.GetComponent<Image>();
            var label = slot.Find(ChildLabel)?.GetComponent<TMP_Text>();
            if (label != null) label.text = def.label;
            if (noCheckSprite == null && check != null) noCheckSprite = check.sprite;

            var item = new Item { check = check, required = def.required, on = false };
            _items.Add(item);

            // 체크 토글 — Check / 라벨 / 빈 칸 어디를 눌러도 토글되도록 각각에 버튼을 단다.
            // (행 1개 버튼만으로는 자식 레이캐스트 전달이 환경에 따라 불안정 → 직접 버튼을 달아 확실히.)
            void Toggle() { item.on = !item.on; Refresh(); }
            WireClick(slot, Toggle);                                 // 빈 칸
            if (check != null) WireClick(check.transform, Toggle);   // 체크박스
            if (label != null) WireClick(label.transform, Toggle);   // 라벨

            // Image(우측 작은 버튼) → 상세 보기. 토글 영역과 겹치지 않는다.
            var imageT = slot.Find(ChildImage);
            if (imageT != null)
            {
                string detail = def.detail;
                WireClick(imageT, () => OpenDetail(detail));
            }
        }
    }

    private static void WireClick(Transform t, UnityEngine.Events.UnityAction action)
    {
        var btn = OnboardingUtil.EnsureButton(t);
        if (btn != null) btn.onClick.AddListener(action);
    }

    private void OpenDetail(string text)
    {
        if (_detailText != null) _detailText.text = text;
        if (_detailPanel != null) _detailPanel.SetActive(true);
    }

    private void CloseDetail()
    {
        if (_detailPanel != null) _detailPanel.SetActive(false);
    }

    private void OnAllAgree()
    {
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
