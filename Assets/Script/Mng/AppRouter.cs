using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 앱의 씬 전환 + "메인 씬에서 어떤 UI를 띄울지"를 관리하는 라우터.
/// Singleton&lt;T&gt; 기반이라 .ins로 어디서나 접근 가능하고 DontDestroyOnLoad로 씬을 넘어 살아남는다.
///
/// 동작 개념:
///   - 메인 씬(Main)에는 UI 프리팹들이 미리 배치돼 있지 않다. 대신 이 라우터가 보관한 프리팹을
///     "대기 화면(pendingScreen) 플래그"에 따라 메인 씬 로드 직후 1개만 생성한다.
///   - 화면 흐름:
///       · MainMenu 버튼 '양치 시작'  → GoBrushing(): Brushing 씬 로드
///       · 양치 종료(BrushingSessionCompleted) → GoMainWith(BrushingFinish): Main 로드 후 결과 화면만
///       · Brushing 씬 Close 버튼      → GoMainWith(MainMenu): Main 로드 후 메뉴만
///
/// 프리팹은 인스펙터에 직접 연결한다(Assets/Prefabs/UI/). 메인 씬에 이 라우터를 1개 둔다.
/// </summary>
public class AppRouter : Singleton<AppRouter>
{
    protected AppRouter() { }

    /// <summary>메인 씬에서 띄울 수 있는 화면 종류.</summary>
    public enum Screen
    {
        None,
        MainMenu,
        BrushingFinish,
    }

    [Header("씬 이름 (Build Settings에 등록돼 있어야 함)")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string brushingSceneName = "Brushing";

    [Header("UI 프리팹 (Assets/Prefabs/UI/)")]
    [Tooltip("메인 메뉴 화면 프리팹(루트 Canvas).")]
    [SerializeField] private GameObject mainMenuPrefab;
    [Tooltip("양치 결과(브러싱 피니쉬) 화면 프리팹(루트 Canvas).")]
    [SerializeField] private GameObject brushingFinishPrefab;

    [Header("바텀 탭 화면 프리팹 (Assets/Prefabs/UI/) — 각 탭이 가리키는 화면")]
    [Tooltip("Home 탭 화면 프리팹(루트 Canvas, 보통 Bottom Tab 포함).")]
    [SerializeField] private GameObject homePrefab;
    [Tooltip("Report 탭 화면 프리팹.")]
    [SerializeField] private GameObject reportPrefab;
    [Tooltip("Buddy 탭 화면 프리팹.")]
    [SerializeField] private GameObject buddyPrefab;
    [Tooltip("Setting 탭 화면 프리팹.")]
    [SerializeField] private GameObject settingPrefab;

    [Header("동작")]
    [Tooltip("앱 최초 시작 시(메인 씬) 자동으로 메뉴 화면을 띄운다.")]
    [SerializeField] private bool showMenuOnStart = true;

    // 다음 메인 씬 로드 시 띄울 화면. 씬 로드 콜백에서 읽고 소비한다.
    private Screen pendingScreen = Screen.None;

    // 현재 메인 씬에 떠 있는 UI 인스턴스(중복 생성 방지 + 교체용).
    private GameObject currentScreenInstance;

    // 가장 최근 양치 결과(점수/구역/연속일수). 결과 화면 생성 시 주입한다.
    private BrushingScoredEvent lastResult;
    private bool hasResult;

    /// <summary>가장 최근 양치 결과가 캐시돼 있는지.</summary>
    public bool HasResult => hasResult;

    /// <summary>가장 최근 양치 결과 — BrushingFinish/Report 화면이 사용.</summary>
    public BrushingScoredEvent LastResult => lastResult;

    /// <summary>현재 표시 중인 바텀 탭. BottomTabBar가 활성 표시를 동기화할 때 참고할 수 있다.</summary>
    public BottomTab CurrentTab { get; private set; } = BottomTab.Home;

    // ─────────────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        // AppRouter는 Main 씬에 배치돼 있고 DontDestroyOnLoad로 영속한다. 그래서 Brushing→Main 재로드 시
        // 씬에 배치된 사본이 다시 생성되어 '중복'이 된다. Singleton<T> 베이스는 중복을 파괴하지 않으므로
        // 여기서 직접 처리한다: 이미 살아있는 인스턴스가 있으면 이 사본은 즉시 비활성화(=Start/OnEnable이
        // 메뉴를 띄우지 않게)하고 파괴한다. 비활성화 후엔 OnEnable/Start가 호출되지 않는다.
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;   // 이후 중복 사본이 곧바로 위 분기로 걸러지도록 즉시 등록.
        base.Awake();      // Singleton의 DontDestroyOnLoad 등록.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnEnable()
    {
        // 양치 종료 → 메인 씬으로 돌아가 결과 화면 표시.
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnBrushingCompleted);
        // 점수 산출 결과 → 결과 화면에 주입할 수 있도록 캐시.
        EventBus.Subscribe<BrushingScoredEvent>(OnScored);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnBrushingCompleted);
        EventBus.Unsubscribe<BrushingScoredEvent>(OnScored);
    }

    private void Start()
    {
        // 앱이 메인 씬에서 시작된 경우, 첫 메뉴를 띄운다.
        // (이미 pendingScreen이 잡혀 있으면 그쪽이 우선 — 씬 로드 콜백이 처리.)
        if (showMenuOnStart && pendingScreen == Screen.None &&
            SceneManager.GetActiveScene().name == mainSceneName)
        {
            ShowScreen(Screen.MainMenu);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 API — 버튼/이벤트가 호출
    // ─────────────────────────────────────────────────────────────

    /// <summary>양치 시작 — Brushing 씬으로 이동.</summary>
    public void GoBrushing()
    {
        pendingScreen = Screen.None;
        SceneManager.LoadScene(brushingSceneName);
    }

    /// <summary>메인 씬으로 이동하고, 로드 직후 메뉴 화면만 표시(Close 버튼용).</summary>
    public void GoMainMenu() => GoMainWith(Screen.MainMenu);

    /// <summary>메인 씬으로 이동하고, 로드 직후 결과 화면만 표시(양치 종료용).</summary>
    public void GoMainFinish() => GoMainWith(Screen.BrushingFinish);

    /// <summary>메인 씬으로 이동하면서 다음에 띄울 화면을 예약한다.</summary>
    public void GoMainWith(Screen screen)
    {
        pendingScreen = screen;

        if (SceneManager.GetActiveScene().name == mainSceneName)
        {
            // 이미 메인 씬이면 씬 로드 콜백이 안 오므로 즉시 화면 전환.
            ConsumePending();
        }
        else
        {
            SceneManager.LoadScene(mainSceneName);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 — 씬 로드 후 대기 화면 소비
    // ─────────────────────────────────────────────────────────────

    private void OnBrushingCompleted(BrushingSessionCompletedEvent e)
    {
        // 양치가 끝나면 메인 씬으로 돌아가 결과 화면만 띄운다.
        GoMainWith(Screen.BrushingFinish);
    }

    // 점수 산출 결과를 캐시 — 결과 화면(ShowScreen)에서 BrushingFinishView로 주입한다.
    // 발행 순서상 이 핸들러는 OnBrushingCompleted가 예약한 씬 로드(프레임 끝)보다 먼저 실행되므로
    // 결과 화면이 만들어질 때 lastResult는 이미 채워져 있다.
    private void OnScored(BrushingScoredEvent e)
    {
        lastResult = e;
        hasResult = true;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        // 메인 씬으로 들어올 때만 화면을 띄운다(Brushing 씬은 자체 UI 사용).
        currentScreenInstance = null; // 씬이 바뀌었으니 이전 인스턴스 참조 폐기.

        if (scene.name != mainSceneName) return;

        if (pendingScreen != Screen.None)
            ConsumePending();
        else if (showMenuOnStart)
            ShowScreen(Screen.MainMenu);
    }

    private void ConsumePending()
    {
        var screen = pendingScreen;
        pendingScreen = Screen.None;
        ShowScreen(screen);
    }

    /// <summary>지정 화면 프리팹을 생성해 표시한다. 기존 화면이 있으면 교체.</summary>
    public void ShowScreen(Screen screen)
    {
        if (currentScreenInstance != null)
        {
            Destroy(currentScreenInstance);
            currentScreenInstance = null;
        }

        var prefab = PrefabFor(screen);
        if (prefab == null)
        {
            if (screen != Screen.None)
                Debug.LogError($"[AppRouter] {screen} 프리팹이 연결되지 않았다 — 인스펙터에서 지정하라.");
            return;
        }

        currentScreenInstance = Instantiate(prefab);
        currentScreenInstance.SetActive(true);

        // 결과 화면이면 가장 최근 점수 결과를 주입한다.
        if (screen == Screen.BrushingFinish && hasResult)
        {
            var view = currentScreenInstance.GetComponentInChildren<BrushingFinishView>(true);
            if (view != null) view.Bind(lastResult);
        }
    }

    private GameObject PrefabFor(Screen screen)
    {
        switch (screen)
        {
            case Screen.MainMenu:       return mainMenuPrefab;
            case Screen.BrushingFinish: return brushingFinishPrefab;
            default:                    return null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 바텀 탭 — 탭이 가리키는 화면 프리팹으로 교체
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 지정 탭의 화면 프리팹을 생성해 표시한다(기존 화면은 교체).
    /// BottomTabBar가 탭 클릭 시 호출한다. 새 화면 프리팹에 포함된 BottomTabBar가
    /// 자기 탭을 활성 표시하므로, 여기서 시각화를 따로 갱신할 필요는 없다.
    /// </summary>
    public void ShowTab(BottomTab tab)
    {
        var prefab = TabPrefab(tab);
        if (prefab == null)
        {
            Debug.LogError($"[AppRouter] {tab} 탭 프리팹이 연결되지 않았다 — 인스펙터에서 지정하라.");
            return;
        }

        CurrentTab = tab;
        pendingScreen = Screen.None;

        if (currentScreenInstance != null)
        {
            Destroy(currentScreenInstance);
            currentScreenInstance = null;
        }

        currentScreenInstance = Instantiate(prefab);
        currentScreenInstance.SetActive(true);
    }

    private GameObject TabPrefab(BottomTab tab)
    {
        switch (tab)
        {
            case BottomTab.Home:    return homePrefab;
            case BottomTab.Report:  return reportPrefab;
            case BottomTab.Buddy:   return buddyPrefab;
            case BottomTab.Setting: return settingPrefab;
            default:                return null;
        }
    }
}
