using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 UI(스크린·팝업) 인스턴스의 생성·전환·파괴를 담당하는 매니저.
/// UI 프리팹은 Resources/UI/Screen, Resources/UI/Popup 경로에서 로드한다.
/// 씬 전환과 무관하게 살아남으며, 전역 Canvas 하나 위에 모든 UI를 배치한다.
/// </summary>
public class UIMng : Singleton<UIMng>
{
    protected UIMng() { }

    // Resources 폴더 기준 UI 프리팹 경로. 경로 변경 시 모든 UI 이동이 필요하므로 상수로 고정.
    private const string SCREEN_PATH = "UI/Screen/";
    private const string POPUP_PATH = "UI/Popup/";

    // 모든 UI가 부모로 삼는 전역 Canvas. Awake에서 자동 생성하고 DontDestroyOnLoad로 유지한다.
    private Canvas rootCanvas;

    // 한 번 로드한 프리팹은 캐시하여 반복 Resources.Load 호출을 막는다.
    // 키는 컴포넌트 Type, 값은 프리팹 GameObject.
    private readonly Dictionary<Type, GameObject> prefabCache = new Dictionary<Type, GameObject>();

    // 현재 화면에 떠 있는 스크린 스택. Push 시 이전 스크린은 OnHide 호출 후 비활성화되어 보존.
    private readonly Stack<UIScreen> screenStack = new Stack<UIScreen>();

    // 현재 떠 있는 팝업 스택. 여러 개가 동시에 쌓일 수 있다 (예: 설정 → 확인 다이얼로그).
    private readonly Stack<UIPopup> popupStack = new Stack<UIPopup>();

    protected override void Awake()
    {
        // 부모 Singleton의 DontDestroyOnLoad 등록.
        base.Awake();

        // 매니저 생성과 함께 루트 Canvas를 준비 — 첫 Show 호출 시 지연 없이 렌더 가능.
        EnsureRootCanvas();
    }

    /// <summary>
    /// 전역 Canvas가 없으면 생성한다.
    /// ScreenSpaceOverlay 모드라 카메라(메뉴 씬/양치 씬 모두 다름)와 무관하게 렌더링된다.
    /// </summary>
    private void EnsureRootCanvas()
    {
        if (rootCanvas != null) return;

        // UI 전용 GameObject를 새로 만들고 씬 전환에도 살아남도록 등록.
        var go = new GameObject("UIRootCanvas");
        DontDestroyOnLoad(go);

        // Canvas 본체 — Overlay 모드이므로 카메라 참조 불필요, sortingOrder로 다른 Canvas와의 순서만 관리.
        rootCanvas = go.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 0;

        // CanvasScaler: 다양한 해상도/DPI에서 일관된 UI 크기를 위해 필수.
        go.AddComponent<CanvasScaler>();

        // GraphicRaycaster: 버튼/이미지 등 UI 입력 처리를 위해 필수.
        go.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// T 타입 스크린을 표시한다. 기존 최상단 스크린이 있으면 비활성·OnHide 처리.
    /// 프리팹은 Resources/UI/Screen/{타입명}.prefab에서 로드된다.
    /// </summary>
    public T ShowScreen<T>() where T : UIScreen
    {
        // 1) 현재 표시 중인 스크린이 있다면 가리고 OnHide 호출.
        //    Destroy하지 않고 비활성만 — Back으로 복귀할 때 인스턴스/상태를 그대로 살리기 위함.
        if (screenStack.Count > 0)
        {
            var top = screenStack.Peek();
            top.OnHide();
            top.gameObject.SetActive(false);
        }

        // 2) 새 스크린 인스턴스화 — 프리팹 캐시를 활용.
        var screen = LoadAndInstantiate<T>(SCREEN_PATH);
        if (screen == null) return null;

        // 3) 스택에 등록 후 활성화·OnShow 호출.
        screenStack.Push(screen);
        screen.gameObject.SetActive(true);
        screen.OnShow();
        return screen;
    }

    /// <summary>
    /// 최상단 스크린을 닫고 직전 스크린을 복귀시킨다.
    /// 스택이 비어 있으면 아무 동작도 하지 않는다.
    /// </summary>
    public void BackScreen()
    {
        if (screenStack.Count == 0) return;

        // 1) 최상단 스크린을 스택에서 빼고 OnHide → 비활성화.
        //    Destroy까지 하지 않는 이유: 다시 Show 됐을 때 데이터 재로드 비용을 피하기 위함.
        var top = screenStack.Pop();
        top.OnHide();
        top.gameObject.SetActive(false);

        // 2) 남은 스택의 새 최상단이 있다면 활성화·OnShow.
        if (screenStack.Count > 0)
        {
            var prev = screenStack.Peek();
            prev.gameObject.SetActive(true);
            prev.OnShow();
        }
    }

    /// <summary>
    /// T 타입 팝업을 띄운다. 스크린과 달리 여러 팝업이 동시에 누적될 수 있다.
    /// 프리팹은 Resources/UI/Popup/{타입명}.prefab에서 로드된다.
    /// </summary>
    public T OpenPopup<T>() where T : UIPopup
    {
        var popup = LoadAndInstantiate<T>(POPUP_PATH);
        if (popup == null) return null;

        popupStack.Push(popup);
        popup.gameObject.SetActive(true);
        popup.OnShow();
        return popup;
    }

    /// <summary>
    /// 최상단 팝업을 닫고 즉시 파괴한다.
    /// 팝업은 보통 일회성이라 인스턴스를 보존하지 않는다 — 다음 OpenPopup이 새로 만든다.
    /// </summary>
    public void ClosePopup()
    {
        if (popupStack.Count == 0) return;

        var top = popupStack.Pop();
        top.OnHide();
        // 메모리 회수를 위해 즉시 Destroy.
        Destroy(top.gameObject);
    }

    /// <summary>
    /// 프리팹을 Resources에서 로드(캐시 우선)하고 rootCanvas 아래에 인스턴스화한다.
    /// 프리팹 이름은 T의 타입명과 동일해야 한다.
    /// 예) TitleScreen 클래스 → Resources/UI/Screen/TitleScreen.prefab
    /// </summary>
    /// <param name="basePath">SCREEN_PATH 또는 POPUP_PATH.</param>
    private T LoadAndInstantiate<T>(string basePath) where T : UIBase
    {
        var type = typeof(T);

        // 1) 캐시 미스 시에만 Resources.Load 호출 — 두 번째부터는 즉시 반환.
        if (!prefabCache.TryGetValue(type, out var prefab))
        {
            var path = basePath + type.Name;
            prefab = Resources.Load<GameObject>(path);

            // 프리팹 누락은 흔한 실수이므로 명확한 에러 메시지를 남긴다 — Resources 경로 그대로 노출.
            if (prefab == null)
            {
                Debug.LogError($"[UIMng] 프리팹을 찾을 수 없음: Resources/{path}.prefab");
                return null;
            }

            prefabCache[type] = prefab;
        }

        // 2) rootCanvas 아래에 인스턴스화. worldPositionStays=false로 로컬 좌표를 0,0으로 유지.
        var instance = Instantiate(prefab, rootCanvas.transform, false);

        // 3) T 컴포넌트가 프리팹 루트에 붙어 있어야 한다 — 없으면 null 반환되어 호출부가 처리.
        return instance.GetComponent<T>();
    }
}
