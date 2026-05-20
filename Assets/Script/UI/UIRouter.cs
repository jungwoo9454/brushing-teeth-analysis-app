using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sesalbeoreut.UI
{
    // 화면 라우터 (Singleton)
    // - Assets/Scenes/UI/ 하위 UXML 자원을 이름으로 로드해 활성 화면을 전환한다.
    // - 백 스택 지원: GoBack()으로 직전 화면으로 돌아간다.
    // - 양치 중 화면(UGUI)은 라우터 밖에서 별도로 활성화한다.
    //
    // 사용법:
    //   UIRouter.ins.Show("Kid/Home");
    //   UIRouter.ins.Show("Parent/PinUnlock");
    //   UIRouter.ins.GoBack();
    public class UIRouter : Singleton<UIRouter>
    {
        // protected 생성자로 비-싱글톤 인스턴스 차단 (CLAUDE.md 컨벤션)
        protected UIRouter() { }

        UIDocument _document;
        VisualElement _root;
        readonly Stack<string> _history = new Stack<string>();
        string _current;

        protected override void Awake()
        {
            base.Awake();

            // UIDocument가 같은 오브젝트나 자식에 있으면 그대로 사용.
            // 없으면 새로 만들어 PanelSettings는 외부에서 할당해야 한다.
            _document = GetComponentInChildren<UIDocument>();
            if (_document == null)
            {
                var go = new GameObject("UIDocument");
                go.transform.SetParent(transform, false);
                _document = go.AddComponent<UIDocument>();
                Debug.LogWarning("[UIRouter] UIDocument를 자동 생성했습니다. PanelSettings를 별도로 할당하세요.");
            }
            _root = _document.rootVisualElement;
        }

        // 화면 전환. screenPath 예: "Kid/Home", "Parent/Dashboard"
        public void Show(string screenPath)
        {
            var asset = Resources.Load<VisualTreeAsset>("UI/" + screenPath);
            if (asset == null)
            {
                Debug.LogError($"[UIRouter] UXML을 찾을 수 없어요: Resources/UI/{screenPath}.uxml");
                return;
            }

            if (!string.IsNullOrEmpty(_current))
                _history.Push(_current);
            _current = screenPath;

            _root = _document.rootVisualElement;
            _root.Clear();
            asset.CloneTree(_root);

            // 현재 테마 적용 (ThemeManager 있으면 동기화)
            if (ThemeManager.ins != null)
                ThemeManager.ins.Apply(_root);
        }

        // 뒤로가기. 스택이 비면 무시.
        public bool GoBack()
        {
            if (_history.Count == 0) return false;
            var prev = _history.Pop();
            // history에 다시 push되지 않도록 _current를 비우고 호출
            _current = null;
            Show(prev);
            return true;
        }

        public string Current => _current;
        public int HistoryCount => _history.Count;
    }
}
