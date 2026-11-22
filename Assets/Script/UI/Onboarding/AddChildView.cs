using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이 프로필 입력 화면(AddChild). AddChild 프리팹 루트(Canvas)에 부착한다.
///
/// 동작:
///   - Name/Input Field에 아이 이름, AgeAndSex/Age/Input Field에 나이를 입력한다.
///   - Sex 아래 Male(남)/Female(여) 중 하나를 택1한다(호칭 선택과 동일한 토글: 선택=이미지 검정/텍스트 흰색).
///   - Next: 이름·성별이 채워지면 저장 + 온보딩 완료 처리 후 메인 메뉴로 이동.
/// </summary>
[DisallowMultipleComponent]
public class AddChildView : MonoBehaviour
{
    [Header("선택 시 색")]
    [SerializeField] private Color selectedImageColor = Color.black;
    [SerializeField] private Color selectedTextColor = Color.white;

    private const string PathName = "RealSpace/Name";
    private const string PathAge  = "RealSpace/AgeAndSex/Age";
    private const string PathSex  = "RealSpace/AgeAndSex/Sex";
    private const string PathNext = "RealSpace/Next";
    private const string PathBack = "RealSpace/Top/Back";

    private static readonly string[] GenderObjects = { "Male", "Female" };

    private class Option
    {
        public Image img; public TMP_Text text; public string value;
        public Color imgColor; public Color textColor;
    }

    private readonly List<Option> _genders = new List<Option>();
    private Option _selected;
    private TMP_InputField _nameInput;
    private TMP_InputField _ageInput;
    private Button _next;

    // 설정 화면에서 팝업으로 띄운 경우: 완료 시 온보딩 대신 콜백으로 목록 갱신·닫기.
    private bool _settingsMode;
    private System.Action _onChildAdded;
    private System.Action _onClose;

    /// <summary>
    /// 설정 화면에서 '아이 추가' 팝업으로 재사용할 때 호출. 완료 시 온보딩 완료/메인 이동 대신
    /// <paramref name="onAdded"/>(목록 갱신)를, Back 시 <paramref name="onClose"/>(닫기)를 호출한다.
    /// (Instantiate 직후 Awake 다음에 호출하면 된다.)
    /// </summary>
    public void ConfigureForSettings(System.Action onAdded, System.Action onClose)
    {
        _settingsMode = true;
        _onChildAdded = onAdded;
        _onClose = onClose;
    }

    private void Awake()
    {
        _nameInput = transform.Find(PathName)?.GetComponentInChildren<TMP_InputField>(true);
        _ageInput  = transform.Find(PathAge)?.GetComponentInChildren<TMP_InputField>(true);
        if (_ageInput != null) _ageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        if (_nameInput != null) _nameInput.onValueChanged.AddListener(_ => RefreshNext());
        if (_ageInput != null)  _ageInput.onValueChanged.AddListener(_ => RefreshNext());

        var sex = transform.Find(PathSex);
        if (sex != null)
        {
            foreach (var objName in GenderObjects)
            {
                var t = sex.Find(objName);
                if (t == null) continue;
                var img = t.GetComponent<Image>();
                var txt = t.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                var opt = new Option
                {
                    img = img,
                    text = txt,
                    value = txt != null ? txt.text : objName,
                    imgColor = img != null ? img.color : Color.white,
                    textColor = txt != null ? txt.color : Color.black,
                };
                _genders.Add(opt);

                var btn = OnboardingUtil.EnsureButton(t);
                if (btn != null) btn.onClick.AddListener(() => Select(opt));
            }
        }

        _next = OnboardingUtil.EnsureButton(transform.Find(PathNext));
        if (_next != null) _next.onClick.AddListener(OnNext);

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(OnBack);

        ApplyVisuals();
        RefreshNext();
    }

    private void Select(Option opt) { _selected = opt; ApplyVisuals(); RefreshNext(); }

    /// <summary>이름·나이(각 1자 이상) + 성별 선택이 모두 충족돼야 Next 활성화.</summary>
    private void RefreshNext()
    {
        if (_next == null) return;
        string name = _nameInput != null ? _nameInput.text.Trim() : "";
        string age  = _ageInput != null ? _ageInput.text.Trim() : "";
        _next.interactable = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(age) && _selected != null;
    }

    private void OnBack()
    {
        // 설정에서 띄운 팝업이면 닫기만, 온보딩이면 이전(PIN) 단계로.
        if (_settingsMode) { _onClose?.Invoke(); return; }
        AppRouter.ins.ShowScreen(AppRouter.Screen.PinInput);
    }

    private void ApplyVisuals()
    {
        foreach (var o in _genders)
        {
            bool sel = o == _selected;
            if (o.img != null)  o.img.color  = sel ? selectedImageColor : o.imgColor;
            if (o.text != null) o.text.color = sel ? selectedTextColor : o.textColor;
        }
    }

    private void OnNext()
    {
        string name = _nameInput != null ? _nameInput.text.Trim() : "";
        string ageStr = _ageInput != null ? _ageInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ageStr) || _selected == null) return;   // 이름·나이·성별 필수.

        int.TryParse(ageStr, out int age);
        UserMng.ins.AddChild(name, age, _selected.value);   // 아이 프로필 추가(첫 아이는 자동 활성)

        if (_settingsMode)
        {
            // 설정에서 추가: 온보딩 완료/메인 이동 없이 목록만 갱신·닫기.
            _onChildAdded?.Invoke();
            return;
        }

        UserMng.ins.CompleteOnboarding();
        AppRouter.ins.ShowScreen(AppRouter.Screen.MainMenu);
    }
}
