using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 온보딩 화면 공통 보조 — 디자인상 클릭 컴포넌트(Button)가 없는 오브젝트(이미지 칩 등)에
/// 런타임으로 Button을 붙여 클릭 가능하게 만든다. (CalendarRangePicker가 칸에 Button을 붙이는 방식과 동일)
/// </summary>
public static class OnboardingUtil
{
    /// <summary>
    /// t에 Button이 있으면 그대로 반환, 없으면 추가해서 반환한다.
    /// 레이캐스트 받을 Graphic이 없으면 투명 Image를 함께 붙인다. t가 null이면 null.
    /// 기존 Button은 설정을 건드리지 않는다.
    /// </summary>
    public static Button EnsureButton(Transform t)
    {
        if (t == null) return null;

        var btn = t.GetComponent<Button>();
        if (btn != null) return btn;

        if (t.GetComponent<Graphic>() == null)
        {
            var img = t.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);   // 투명 — 보이진 않지만 클릭(레이캐스트)은 받는다.
        }

        btn = t.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        return btn;
    }
}
