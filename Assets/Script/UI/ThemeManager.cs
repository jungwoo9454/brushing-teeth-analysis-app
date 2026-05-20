using UnityEngine;
using UnityEngine.UIElements;

namespace Sesalbeoreut.UI
{
    // 테마 매니저 (Singleton)
    // - 라이트/다크 테마를 .theme-light / .theme-dark 클래스로 전환한다.
    // - UI Toolkit 화면 루트에 클래스를 토글하는 방식. 토큰(USS)이 알아서 색을 바꾼다.
    //
    // 사용법:
    //   ThemeManager.ins.SetTheme(Theme.Dark);
    //   ThemeManager.ins.Toggle();
    public class ThemeManager : Singleton<ThemeManager>
    {
        protected ThemeManager() { }

        public enum Theme { Light, Dark }

        [SerializeField] Theme _current = Theme.Light;
        public Theme Current => _current;

        public void SetTheme(Theme theme)
        {
            _current = theme;

            // 활성 화면이 있으면 즉시 갱신
            if (UIRouter.ins != null)
            {
                var doc = UIRouter.ins.GetComponentInChildren<UIDocument>();
                if (doc != null) Apply(doc.rootVisualElement);
            }
        }

        public void Toggle()
        {
            SetTheme(_current == Theme.Light ? Theme.Dark : Theme.Light);
        }

        // 주어진 루트에 현재 테마 클래스를 적용. UIRouter.Show 직후에도 호출됨.
        public void Apply(VisualElement root)
        {
            if (root == null) return;

            // 화면 내 .scr 루트(또는 root 자체)에 테마 클래스를 토글.
            ApplyToElement(root);
            var scr = root.Q<VisualElement>(className: "scr");
            if (scr != null) ApplyToElement(scr);
        }

        void ApplyToElement(VisualElement el)
        {
            if (_current == Theme.Light)
            {
                el.AddToClassList("theme-light");
                el.RemoveFromClassList("theme-dark");
            }
            else
            {
                el.AddToClassList("theme-dark");
                el.RemoveFromClassList("theme-light");
            }
        }
    }
}
