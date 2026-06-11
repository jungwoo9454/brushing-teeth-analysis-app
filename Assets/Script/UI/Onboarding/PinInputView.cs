using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보호자 모드 PIN 설정 화면(PinInput). PinInput 프리팹 루트(Canvas)에 부착한다.
///
/// 동작:
///   - Top/Back/TextInfo/SubTitle 텍스트를 "보호자 모드, 핀 설정"으로 설정.
///   - Num pad의 0~9 버튼을 누르면 PIN이 채워지고, PIN/Pin*/Indicator가 차례로 활성화된다.
///   - Backspace로 한 자리 지운다.
///   - pinLength(기본 4)자리가 다 차면 저장하고 아이 정보(AddChild) 화면으로 이동.
///
/// (이 화면은 'PIN 설정'용이다. 추후 '보호자 모드 잠금 해제'에도 재사용하려면 모드 플래그를 추가하면 된다.)
/// </summary>
[DisallowMultipleComponent]
public class PinInputView : MonoBehaviour
{
    [SerializeField] private int pinLength = 4;
    [SerializeField] private string subtitle = "보호자 모드, 핀 설정";

    [Header("인디케이터 팝 연출 (LeanTween)")]
    [Tooltip("채워질 때 시작 스케일(작게 시작해 1로 커진다).")]
    [SerializeField, Min(0f)] private float popStartScale = 0.4f;
    [Tooltip("팝 애니메이션 시간(초).")]
    [SerializeField, Min(0.01f)] private float popTime = 0.25f;
    [Tooltip("마지막 자리 입력 후 다음 화면으로 넘어가기까지 대기(마지막 팝을 보여주기 위함).")]
    [SerializeField, Min(0f)] private float completeDelay = 0.35f;

    private const string PathSubtitle = "RealSpace/Top/Back/TextInfo/SubTitle";
    private const string PathPin      = "RealSpace/PIN";
    private const string PathNumpad   = "RealSpace/Num pad";
    private const string PathBack     = "RealSpace/Top/Back";

    private readonly List<GameObject> _indicators = new List<GameObject>();
    private readonly StringBuilder _pin = new StringBuilder();

    private void Awake()
    {
        var sub = transform.Find(PathSubtitle)?.GetComponent<TMP_Text>();
        if (sub != null) sub.text = subtitle;

        // PIN 인디케이터 — PIN/Pin*/Indicator 순서대로 수집하고 모두 끈다.
        var pinRoot = transform.Find(PathPin);
        if (pinRoot != null)
        {
            foreach (Transform slot in pinRoot)
            {
                var ind = slot.Find("Indicator");
                if (ind == null) continue;
                _indicators.Add(ind.gameObject);
                ind.gameObject.SetActive(false);
            }
        }

        // 넘패드 — 숫자 라벨이 있으면 숫자 버튼, 이름에 'Back'이 들어가면 백스페이스.
        var numpad = transform.Find(PathNumpad);
        if (numpad != null)
        {
            foreach (Transform key in numpad)
            {
                var btn = key.GetComponent<Button>();
                if (btn == null) continue;   // Freespace 등 버튼 없는 칸은 무시.

                string digit = key.Find("Text (TMP)")?.GetComponent<TMP_Text>()?.text?.Trim() ?? "";
                if (digit.Length == 1 && char.IsDigit(digit[0]))
                {
                    string d = digit;
                    btn.onClick.AddListener(() => Append(d));
                }
                else if (key.name.Contains("Back"))
                {
                    btn.onClick.AddListener(Backspace);
                }
            }
        }

        var back = OnboardingUtil.EnsureButton(transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.ParentInfo));

        UpdateIndicators();
    }

    private void Append(string d)
    {
        if (_pin.Length >= pinLength) return;
        _pin.Append(d);
        int idx = _pin.Length - 1;     // 이번에 채워진 자리.
        UpdateIndicators();
        PopIndicator(idx);
        if (_pin.Length >= pinLength)
            LeanTween.delayedCall(gameObject, completeDelay, Complete);   // 마지막 팝을 보여준 뒤 전환.
    }

    /// <summary>방금 채워진 인디케이터를 작게 시작해 1로 커지는 팝 연출(LeanTween, OutBack 오버슈트).</summary>
    private void PopIndicator(int idx)
    {
        if (idx < 0 || idx >= _indicators.Count) return;
        var go = _indicators[idx];
        if (go == null) return;

        LeanTween.cancel(go);
        go.transform.localScale = Vector3.one * popStartScale;
        LeanTween.scale(go, Vector3.one, popTime).setEaseOutBack();
    }

    private void Backspace()
    {
        if (_pin.Length == 0) return;
        _pin.Remove(_pin.Length - 1, 1);
        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        for (int i = 0; i < _indicators.Count; i++)
            if (_indicators[i] != null) _indicators[i].SetActive(i < _pin.Length);
    }

    private void Complete()
    {
        UserMng.ins.SetPin(_pin.ToString());
        AppRouter.ins.ShowScreen(AppRouter.Screen.AddChild);
    }
}
