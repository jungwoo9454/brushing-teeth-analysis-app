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
@"<b>[만 14세 이상 및 보호자 동의]</b>

세살버릇은 아이의 올바른 양치 습관 형성을 돕는 서비스로, 보호자가 계정을 만들고 아이와 함께 이용하는 것을 전제로 합니다.

1. 본 앱의 계정은 만 14세 이상인 보호자(부모, 조부모, 교사 등 아이를 실제로 돌보는 사람)가 생성하고 관리합니다.

2. 보호자는 아이의 법정대리인이거나 법정대리인의 동의를 받은 사람으로서, 아이의 정보(이름, 나이, 성별)를 등록하고 아이가 앱을 이용하는 것에 동의합니다.

3. 만 14세 미만 아동은 보호자의 지도와 감독 아래 앱을 이용하며, 보호자는 이용 시간과 사용 환경(욕실 내 기기 거치, 미끄러짐, 침수 주의 등)을 관리할 책임이 있습니다.

4. 보호자 모드는 보호자가 설정한 4자리 PIN으로 보호되며, 보호자는 PIN이 아이나 제3자에게 노출되지 않도록 관리해야 합니다.

5. 본 항목에 동의하지 않으면 서비스 가입 및 이용이 제한됩니다."
        },
        new ClauseDef
        {
            label = "<b><color=#3B7FE8>[필수]</color></b> 서비스 이용약관 동의",
            required = true,
            detail =
@"<b>[서비스 이용약관]</b>

<b>제1조 (목적)</b>
이 약관은 '세살버릇'(이하 ""앱"")이 제공하는 양치 코칭 서비스(이하 ""서비스"")의 이용과 관련하여 앱과 이용자의 권리, 의무 및 책임 사항을 정하는 것을 목적으로 합니다.

<b>제2조 (용어의 정의)</b>
1. ""이용자""란 이 약관에 동의하고 서비스를 이용하는 보호자와, 보호자의 지도 아래 서비스를 이용하는 아이를 말합니다.
2. ""보호자""란 계정을 생성하고 아이 프로필을 등록하고 관리하는 만 14세 이상의 사람을 말합니다.
3. ""아이 프로필""이란 보호자가 등록한 아이별 이용 정보(이름, 나이, 성별, 양치 기록, 획득 스티커 등)를 말합니다.

<b>제3조 (약관의 효력 및 변경)</b>
1. 이 약관은 앱 화면에 게시하고 이용자가 동의함으로써 효력이 발생합니다.
2. 앱은 관련 법령을 위반하지 않는 범위에서 약관을 변경할 수 있으며, 변경 시 적용일 7일 전부터(이용자에게 불리한 변경은 30일 전부터) 앱 내 공지로 안내합니다.
3. 이용자가 변경된 약관에 동의하지 않는 경우 서비스 이용을 중단할 수 있습니다.

<b>제4조 (서비스의 내용)</b>
앱은 다음 서비스를 제공합니다.
1. 카메라와 온디바이스 AI를 이용한 양치 동작(양치 부위) 분석
2. 양치 중 AR 피드백(세균이 사라지는 효과 등)과 음성 및 화면 안내
3. 양치 기록, 리포트, 보상(스티커) 제공
4. 보호자 모드를 통한 아이별 기록 확인 및 관리

<b>제5조 (서비스의 이용)</b>
1. 서비스는 연중무휴, 1일 24시간 이용할 수 있음을 원칙으로 합니다. 다만 기기 사양, 운영체제 버전, 카메라 상태에 따라 일부 기능이 제한될 수 있습니다.
2. 정확한 분석을 위해 얼굴과 칫솔이 카메라에 잘 보이도록 밝은 곳에서 이용해야 합니다.

<b>제6조 (카메라 이용)</b>
1. 앱은 양치 분석을 위해 기기의 카메라 접근 권한을 요청하며, 권한을 허용하지 않으면 양치 분석 기능을 이용할 수 없습니다.
2. 카메라 영상은 기기 안에서 실시간으로 분석되며, 영상 자체를 저장하거나 외부로 전송하지 않습니다.

<b>제7조 (이용자의 의무)</b>
1. 이용자는 서비스를 본래 목적(양치 습관 형성)에 맞게 이용해야 합니다.
2. 이용자는 앱을 무단으로 복제, 변경, 배포하거나 역설계(리버스 엔지니어링)해서는 안 됩니다.
3. 보호자는 아이가 안전한 환경에서 앱을 이용하도록 지도해야 하며, 물기가 있는 곳에 기기를 둘 때 낙하나 침수에 주의해야 합니다.

<b>제8조 (데이터의 저장과 관리)</b>
1. 보호자와 아이 정보와 양치 기록 등 서비스 이용 데이터는 이용자의 기기 안에만 저장됩니다.
2. 앱 삭제, 기기 초기화 또는 분실 시 저장된 데이터는 복구할 수 없으니 유의하시기 바랍니다.

<b>제9조 (서비스의 변경 및 중단)</b>
앱은 기능 개선, 운영상 또는 기술상 필요에 따라 서비스의 일부 또는 전부를 변경하거나 중단할 수 있으며, 중요한 변경은 앱 내 공지 또는 업데이트 안내로 알립니다.

<b>제10조 (책임의 한계)</b>
1. 앱이 제공하는 분석, 피드백, 리포트는 양치 습관 형성을 돕기 위한 참고 정보이며, 치과 진료나 의학적 진단이나 치료를 대체하지 않습니다. 구강 건강에 이상이 있으면 치과 전문의와 상담하시기 바랍니다.
2. AI 분석 결과는 촬영 환경(조명, 각도, 거리 등)에 따라 실제와 다를 수 있습니다.
3. 앱은 관련 법령이 허용하는 범위에서, 이용자의 기기 관리 소홀 등 이용자의 귀책 사유로 발생한 데이터 손실이나 손해에 대해 책임을 지지 않습니다.

<b>제11조 (준거법 및 분쟁 해결)</b>
이 약관은 대한민국 법령에 따라 해석되며, 서비스 이용과 관련한 분쟁은 민사소송법에 따른 관할 법원에서 해결합니다.

<b>부칙</b>
이 약관은 2026년 11월 22일부터 시행합니다."
        },
        new ClauseDef
        {
            label = "<b><color=#3B7FE8>[필수]</color></b> 개인정보 처리방침 동의",
            required = true,
            detail =
@"<b>[개인정보 처리방침]</b>

'세살버릇'(이하 ""앱"")은 개인정보 보호법 등 관련 법령을 준수하며, 이용자의 개인정보를 다음과 같이 처리합니다.

<b>1. 개인정보의 처리 목적</b>
- 보호자 및 아이 프로필 생성 및 관리
- 보호자 모드 접근 제한(PIN 확인)
- 양치 분석, 기록, 리포트, 보상(스티커) 제공

<b>2. 처리하는 개인정보 항목</b>
- 보호자: 이름, 호칭(엄마, 아빠, 조부모, 선생님), 보호자 모드 PIN(4자리)
- 아이: 이름, 나이, 성별
- 이용 기록: 양치 일시, 양치 시간, 부위별 양치 결과와 점수, 획득 스티커

<b>3. 카메라 영상의 처리</b>
양치 분석을 위해 카메라 영상을 이용하지만, 영상은 기기 안에서 실시간으로 처리된 뒤 바로 폐기됩니다. 영상이나 사진을 저장하거나 외부 서버로 전송하지 않으며, 분석 중 계산되는 입과 칫솔의 위치 값도 저장하지 않습니다.

<b>4. 개인정보의 저장 위치 및 보유 기간</b>
- 모든 개인정보는 이용자의 기기(앱 전용 저장 공간)에만 저장되며, 앱은 별도의 서버로 개인정보를 수집하지 않습니다.
- 개인정보는 이용자가 앱을 삭제할 때까지 기기에 보관됩니다.

<b>5. 개인정보의 파기</b>
앱을 삭제하면 기기에 저장된 개인정보와 양치 기록이 함께 삭제됩니다.

<b>6. 개인정보의 제3자 제공 및 처리 위탁</b>
앱은 이용자의 개인정보를 제3자에게 제공하거나 외부에 처리를 위탁하지 않습니다.

<b>7. 만 14세 미만 아동의 개인정보</b>
아이의 개인정보는 법정대리인(보호자)의 동의를 받아 보호자가 직접 등록하며, 서비스 제공에 필요한 최소한의 정보만 처리합니다.

<b>8. 정보주체와 법정대리인의 권리</b>
이용자와 법정대리인은 앱 화면에서 등록된 정보를 확인할 수 있으며, 앱을 삭제하여 언제든지 모든 정보의 처리를 중단하고 삭제할 수 있습니다.

<b>9. 개인정보의 안전성 확보 조치</b>
개인정보는 다른 앱이 접근할 수 없는 앱 전용 저장 공간에 저장되며, 보호자 모드는 PIN으로 보호됩니다.

<b>10. 처리방침의 변경</b>
이 처리방침이 변경되는 경우 앱 내 공지로 안내합니다. 향후 서버 연동 등으로 개인정보를 외부로 전송하게 되는 경우에는 사전에 별도 동의를 받습니다.

<b>부칙</b>
이 처리방침은 2026년 11월 22일부터 시행합니다."
        },
        new ClauseDef
        {
            label = "<b><color=#8B8F94>[선택]</color></b> 알림 수신 동의",
            required = false,
            detail =
@"<b>[알림 수신 동의 (선택)]</b>

1. 수신 목적
양치 시간 알림, 연속 양치 기록 축하, 리포트 확인 안내 등 올바른 양치 습관 형성에 도움이 되는 정보를 보내 드립니다.

2. 발송 방식
기기 안에서 예약되어 발송되는 로컬 알림이며, 알림 발송을 위해 개인정보를 외부로 전송하지 않습니다.

3. 광고성 정보
광고성 정보를 보내는 경우에는 별도로 동의를 받습니다.

4. 동의 거부 및 철회
선택 항목이므로 동의하지 않아도 서비스 이용에 제한이 없습니다. 동의한 후에도 기기의 알림 설정에서 언제든지 알림 수신을 끌 수 있습니다."
        },
    };

    private const string PathClauses  = "RealSpace/Scroll View/Viewport/Content/clauses";
    private const string PathAllAgree = "RealSpace/All Agree";
    private const string PathContinue = "RealSpace/Continue agree";
    private const string PathBack     = "RealSpace/Top/Back";
    // 상세 팝업은 RealSpace(좌우·상하 여백이 있는 영역)가 아니라 루트 캔버스 바로 아래에 둔다 —
    // 그래야 반투명 검은 배경이 화면 전체를 덮는다.
    private const string PathDetail   = "Clause Detail";
    private const string PathDetailScroll = "Clause Detail/Scroll View";
    private const string PathDetailText = "Clause Detail/Scroll View/Viewport/Content/Text (TMP)";
    private const string PathDetailClose = "Clause Detail/Close Text";

    private const string ChildCheck = "Check";
    private const string ChildImage = "Image";       // 약관별 '상세 보기' 버튼
    private const string ChildLabel = "Text (TMP)";

    private class Item { public Image check; public bool required; public bool on; }

    private readonly List<Item> _items = new List<Item>();
    private Image _allCheck;
    private Button _continue;
    private GameObject _detailPanel;
    private ScrollRect _detailScroll;
    private TMP_Text _detailText;

    private void Awake()
    {
        _continue = transform.Find(PathContinue)?.GetComponent<Button>();

        // 상세 팝업.
        _detailPanel  = transform.Find(PathDetail)?.gameObject;
        _detailScroll = transform.Find(PathDetailScroll)?.GetComponent<ScrollRect>();
        _detailText   = transform.Find(PathDetailText)?.GetComponent<TMP_Text>();
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

        // 이전에 본 약관의 스크롤 위치가 남지 않도록 항상 맨 위부터 보여준다.
        if (_detailScroll != null)
        {
            _detailScroll.StopMovement();
            if (_detailScroll.content != null) _detailScroll.content.anchoredPosition = Vector2.zero;
        }
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
