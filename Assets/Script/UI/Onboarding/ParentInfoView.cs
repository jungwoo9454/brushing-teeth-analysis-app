using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보호자 정보 입력 화면(ParentInfo). ParentInfo 프리팹 루트(Canvas)에 부착한다.
///
/// 동작:
///   - Name/Input Field에 보호자 이름을 입력한다.
///   - Prefix 아래 호칭 칩 4개(엄마/아빠/조부모/선생님) 중 하나를 택1한다.
///     선택된 칩은 이미지=검정, 텍스트=흰색으로 바뀌고, 나머지는 원래 색으로 돌아간다.
///   - Next: 이름과 호칭이 모두 입력되면 저장하고 PIN 설정(PinInput) 화면으로 이동.
/// </summary>
[DisallowMultipleComponent]
public class ParentInfoView : MonoBehaviour
{
    [Header("선택 시 색")]
    [SerializeField] private Color selectedImageColor = Color.black;
    [SerializeField] private Color selectedTextColor = Color.white;

    private const string PathName   = "RealSpace/Name";
    private const string PathPrefix = "RealSpace/Prefix";
    private const string PathNext   = "RealSpace/Next";
    private const string PathBack   = "RealSpace/Top/Back";

    private static readonly string[] PrefixObjects = { "Prefix", "Prefix (1)", "Prefix (2)", "Prefix (3)" };

    private class Option
    {
        public Image img; public TMP_Text text; public string value;
        public Color imgColor; public Color textColor;
    }

    private readonly List<Option> _prefixes = new List<Option>();
    private Option _selected;
    private TMP_InputField _nameInput;
    private Button _next;

    private void Awake()
    {
        _nameInput = transform.Find(PathName)?.GetComponentInChildren<TMP_InputField>(true);
        if (_nameInput != null) _nameInput.onValueChanged.AddListener(_ => RefreshNext());

        var prefixRoot = transform.Find(PathPrefix);
        if (prefixRoot != null)
        {
            foreach (var objName in PrefixObjects)
            {
                var t = prefixRoot.Find(objName);
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
                _prefixes.Add(opt);

                var btn = OnboardingUtil.EnsureButton(t);
                if (btn != null) btn.onClick.AddListener(() => Select(opt));
            }
        }

        _next = OnboardingUtil.EnsureButton(transform.Find(PathNext));
        if (_next != null) _next.onClick.AddListener(OnNext);

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.Clause));

        ApplyVisuals();
        RefreshNext();
    }

    private void Select(Option opt) { _selected = opt; ApplyVisuals(); RefreshNext(); }

    /// <summary>이름 입력(1자 이상) + 호칭 선택이 모두 충족돼야 Next 활성화.</summary>
    private void RefreshNext()
    {
        if (_next == null) return;
        string name = _nameInput != null ? _nameInput.text.Trim() : "";
        _next.interactable = !string.IsNullOrEmpty(name) && _selected != null;
    }

    private void ApplyVisuals()
    {
        foreach (var o in _prefixes)
        {
            bool sel = o == _selected;
            if (o.img != null)  o.img.color  = sel ? selectedImageColor : o.imgColor;
            if (o.text != null) o.text.color = sel ? selectedTextColor : o.textColor;
        }
    }

    private void OnNext()
    {
        string name = _nameInput != null ? _nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name) || _selected == null) return;   // 이름·호칭 둘 다 필수.

        UserMng.ins.SetGuardian(name, _selected.value);
        AppRouter.ins.ShowScreen(AppRouter.Screen.PinInput);
    }
}
