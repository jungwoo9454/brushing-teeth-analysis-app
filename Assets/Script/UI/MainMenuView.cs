using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 화면의 데이터 바인딩. Mainmenu 프리팹 루트(Canvas)에 붙인다.
///
/// 담당:
///   1) 날짜(연도 제외 "M월 d일 요일요일") + 인사말("안녕, {이름}!") — 이름은 UserMng.ins.UserName(기본 하준).
///   2) To-do(아침/점심/저녁): 오늘 해당 시간대 양치 기록이 있으면 Checked를 완료 스프라이트로 + "HH:mm 완료",
///      없으면 미완료 스프라이트 + "오전 6시 ~ 오전 10시 양치 완료 예정" 식 안내.
///   3) 스코어: 이번 주(월~현재) 평균 점수 표시. 전주 평균 대비 상승=초록 유지/위 화살표,
///      하락=빨강(초록을 HSV로 색상만 빨강으로) + 화살표 Scale.Y 음수로 뒤집기, 변동량 표기.
///
/// 자식 참조는 경로로 자동 탐색하므로(비우면), 프리팹에는 이 컴포넌트 + 체크 스프라이트 2개만 지정하면 된다.
/// 색상/화살표 기본 상태는 Awake에서 캡처해 "상승" 상태로 사용하고, "하락"은 거기서 빨강으로 연산한다.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuView : MonoBehaviour
{
    [Header("프로필 / 날짜 (비우면 경로로 자동 탐색)")]
    [SerializeField] private TMP_Text todayText;
    [SerializeField] private TMP_Text helloText;

    [Header("To-do List")]
    [SerializeField] private Transform todolistRoot;
    [Tooltip("완료 체크 스프라이트 (check.png).")]
    [SerializeField] private Sprite checkedSprite;
    [Tooltip("미완료 체크 스프라이트 (check_empty.png).")]
    [SerializeField] private Sprite uncheckedSprite;

    [Header("스코어 / 전주 대비 (비우면 경로로 자동 탐색)")]
    [SerializeField] private TMP_Text scoreText;   // 이번 주 평균 점수(큰 숫자) — 값만 갱신
    [SerializeField] private Image lastWeekScoreImage; // 전주 대비 라벨 배경(Last week/Score) — 상승=초록/하락=빨강
    [SerializeField] private TMP_Text deltaText;   // 전주 대비 변동량
    [SerializeField] private Image arrowImage;     // 상승/하락 화살표

    [Header("Alert report (가장 부족한 구역, 비우면 경로로 자동 탐색)")]
    [SerializeField] private Image alertBackground;    // Alert report 루트 pill (경고색 배경)
    [SerializeField] private Image alertBadge;         // Amount Arrow 배지 (경고색)
    [SerializeField] private TMP_Text alertZoneText;   // Teeth location (부족 구역명)
    [SerializeField] private TMP_Text alertReportText; // report (상태 메시지)

    [Header("모드별 패널 (비우면 경로로 자동 탐색)")]
    [Tooltip("아이 모드에서 표시 — 양치 시작 패널.")]
    [SerializeField] private GameObject brushingStartPanel;
    [Tooltip("보호자 모드에서 표시 — 아이 목록(가로 스크롤).")]
    [SerializeField] private GameObject childListPanel;

    // 루트(Mainmenu) 기준 상대 경로
    private const string PathToday    = "RealSpace/Top/Profile/TextInfo/Today";
    private const string PathHello    = "RealSpace/Top/Profile/TextInfo/hello ment";
    private const string PathTodolist = "RealSpace/SubPannels/Sub pannel/Todolist";
    private const string PathScore    = "RealSpace/SubPannels/Sub pannel (1)/Score";
    private const string PathLastWeek = "RealSpace/SubPannels/Sub pannel (1)/Last week/Score";
    private const string PathDelta    = "RealSpace/SubPannels/Sub pannel (1)/Last week/Score/Text (TMP)";
    private const string PathArrow    = "RealSpace/SubPannels/Sub pannel (1)/Last week/Score/Arrow";
    private const string PathAlertBg     = "RealSpace/Alert report";
    private const string PathAlertBadge  = "RealSpace/Alert report/Amount Arrow";
    private const string PathAlertZone   = "RealSpace/Alert report/TextArea/Teeth location";
    private const string PathAlertReport = "RealSpace/Alert report/TextArea/report";

    private const string PathBrushingPanel    = "RealSpace/Brushing start pannel";
    private const string PathChildList        = "RealSpace/ChildList";
    private const string PathChildSlotContent = "RealSpace/ChildList/Viewport/Content";
    private const string PathChildSlotTemplate = PathChildSlotContent + "/Child slot";
    private const string ChildSlotNamePath = "Text (TMP) (1)";   // 이름
    private const string ChildSlotAgePath  = "Text (TMP)";       // 나이

    // Todo 항목 내부 자식 경로
    private const string ChildChecked  = "Checked";
    private const string ChildTitle    = "Text Area/TItle";
    private const string ChildComplete = "Text Area/isComplete";

    private static readonly string[] Weekdays = { "일", "월", "화", "수", "목", "금", "토" };

    // UiZone(0~4) → Alert용 구역명("어금니" 스타일, 디자인 예시에 맞춤).
    private static readonly string[] ZoneNames =
        { "위 왼쪽 어금니", "위 오른쪽 어금니", "아래 왼쪽 어금니", "아래 오른쪽 어금니", "앞니" };

    // 상승(=현재 초록) 원본 — Awake에서 1회 캡처. 하락 시 여기서 빨강을 연산한다.
    private Color _upPillColor;
    private Color _upDeltaColor;
    private Color _upArrowColor;
    private Vector3 _arrowScaleUp = Vector3.one;

    // Alert report 원본(경고=빨강/주황 계열). 양호 시 여기서 초록을 연산한다.
    private Color _alertBgColor;
    private Color _alertBadgeColor;

    private bool _captured;

    // 보호자 모드 아이 목록(가로 슬롯) 런타임 상태.
    private Transform _childSlotContent;
    private Transform _childSlotTemplate;
    private Color _childSlotNormalColor = Color.white;
    private readonly List<GameObject> _childSlots = new List<GameObject>();

    private void Awake()
    {
        ResolveRefs();
        Capture();
    }

    private void OnEnable() => Refresh();

    // ── 참조 / 캡처 ───────────────────────────────────────────────

    private void ResolveRefs()
    {
        if (todayText == null) todayText = FindTMP(PathToday);
        if (helloText == null) helloText = FindTMP(PathHello);
        if (todolistRoot == null) todolistRoot = transform.Find(PathTodolist);
        if (scoreText == null) scoreText = FindTMP(PathScore);
        if (deltaText == null) deltaText = FindTMP(PathDelta);
        if (arrowImage == null)
        {
            var t = transform.Find(PathArrow);
            if (t != null) arrowImage = t.GetComponent<Image>();
        }
        if (lastWeekScoreImage == null)
        {
            var t = transform.Find(PathLastWeek);
            if (t != null) lastWeekScoreImage = t.GetComponent<Image>();
        }
        if (alertBackground == null)
        {
            var t = transform.Find(PathAlertBg);
            if (t != null) alertBackground = t.GetComponent<Image>();
        }
        if (alertBadge == null)
        {
            var t = transform.Find(PathAlertBadge);
            if (t != null) alertBadge = t.GetComponent<Image>();
        }
        if (alertZoneText == null) alertZoneText = FindTMP(PathAlertZone);
        if (alertReportText == null) alertReportText = FindTMP(PathAlertReport);

        if (brushingStartPanel == null)
        {
            var t = transform.Find(PathBrushingPanel);
            if (t != null) brushingStartPanel = t.gameObject;
        }
        if (childListPanel == null)
        {
            var t = transform.Find(PathChildList);
            if (t != null) childListPanel = t.gameObject;
        }
        _childSlotContent = transform.Find(PathChildSlotContent);
        _childSlotTemplate = transform.Find(PathChildSlotTemplate);
        if (_childSlotTemplate != null)
        {
            var img = _childSlotTemplate.GetComponent<Image>();
            if (img != null) _childSlotNormalColor = img.color;
            _childSlotTemplate.gameObject.SetActive(false);   // 복제용 프로토타입 — 숨긴다.
        }
    }

    private TMP_Text FindTMP(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private void Capture()
    {
        if (_captured) return;
        if (lastWeekScoreImage != null) _upPillColor = lastWeekScoreImage.color;
        if (deltaText != null) _upDeltaColor = deltaText.color;
        if (arrowImage != null)
        {
            _upArrowColor = arrowImage.color;
            _arrowScaleUp = arrowImage.rectTransform.localScale;
        }
        if (alertBackground != null) _alertBgColor = alertBackground.color;
        if (alertBadge != null) _alertBadgeColor = alertBadge.color;
        _captured = true;
    }

    // ── 갱신 ──────────────────────────────────────────────────────

    private void Refresh()
    {
        var now = DateTime.Now;
        ApplyMode();           // 모드별 패널(양치 시작 ↔ 아이 목록) 토글.
        RefreshProfile(now);

        var records = LoadRecords();   // 활성 아이 기준 — SubPannels/Alert가 선택된 아이 데이터를 표시.
        RefreshTodo(records, now);
        RefreshScore(records, now);
        RefreshAlert(records, now);
    }

    // ── 모드별 구성 (아이 / 보호자) ───────────────────────────────

    /// <summary>
    /// 보호자 모드: 양치 시작 패널을 끄고 아이 목록을 켜 아이들을 보여준다(클릭으로 아이 전환).
    /// 아이 모드: 아이 목록을 끄고 양치 시작 패널을 켠다(기존 메인 메뉴).
    /// 하단 SubPannels/Alert report는 두 모드 모두 '선택된 아이' 데이터를 그대로 표시한다.
    /// </summary>
    private void ApplyMode()
    {
        bool guardian = UserMng.ins != null && UserMng.ins.IsGuardian;
        if (brushingStartPanel != null) brushingStartPanel.SetActive(!guardian);
        if (childListPanel != null) childListPanel.SetActive(guardian);
        if (guardian) BuildChildList();
    }

    /// <summary>보호자 모드 아이 목록 — 등록된 아이 수만큼 'Child slot'을 복제, 활성 아이 강조, 클릭 시 아이 전환.</summary>
    private void BuildChildList()
    {
        if (_childSlotContent == null || _childSlotTemplate == null) return;

        foreach (var go in _childSlots) if (go != null) Destroy(go);
        _childSlots.Clear();

        var children = UserMng.ins != null ? UserMng.ins.Children : null;
        var active = UserMng.ins != null ? UserMng.ins.ActiveChild : null;
        if (children == null) return;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var go = Instantiate(_childSlotTemplate.gameObject, _childSlotContent);
            go.SetActive(true);
            go.name = "Child slot_" + child.id;
            _childSlots.Add(go);

            var nameText = go.transform.Find(ChildSlotNamePath)?.GetComponent<TMP_Text>();
            if (nameText != null) nameText.text = child.name;

            var ageText = go.transform.Find(ChildSlotAgePath)?.GetComponent<TMP_Text>();
            if (ageText != null) ageText.text = child.age > 0 ? $"{child.age}세" : "";

            bool isActive = active != null && child.id == active.id;
            var slotImg = go.GetComponent<Image>();
            if (slotImg != null)
            {
                var c = _childSlotNormalColor;
                if (!isActive) c.a *= 0.45f;
                slotImg.color = c;
            }

            string id = child.id;
            var btn = OnboardingUtil.EnsureButton(go.transform);
            if (btn != null) btn.onClick.AddListener(() => OnSelectChild(id));
        }
    }

    // 보호자 모드에서 아이 전환 — 활성 아이만 바꾸고(모드는 보호자 유지) 화면을 그 아이 데이터로 갱신.
    private void OnSelectChild(string childId)
    {
        if (UserMng.ins == null) return;
        UserMng.ins.SetActiveChild(childId);
        Refresh();
    }

    private void RefreshProfile(DateTime now)
    {
        if (todayText != null)
            todayText.text = $"{now.Month}월 {now.Day}일 {Weekdays[(int)now.DayOfWeek]}요일";

        if (helloText != null)
        {
            string name = UserMng.ins != null ? UserMng.ins.UserName : null;
            if (string.IsNullOrEmpty(name)) name = "하준";
            helloText.text = $"안녕, {name}!";
        }
    }

    // 활성 아이의 기록만 — 홈 화면 점수/체크리스트/알림도 현재 아이 기준.
    private List<BrushingRecord> LoadRecords()
    {
        try { return ChildRecords.ActiveChildRecords(new JsonBrushingRecordRepository().LoadAll()); }
        catch { return new List<BrushingRecord>(); }
    }

    // ── To-do List ────────────────────────────────────────────────

    private void RefreshTodo(List<BrushingRecord> records, DateTime now)
    {
        if (todolistRoot == null) return;

        for (int i = 0; i < todolistRoot.childCount; i++)
        {
            var item = todolistRoot.GetChild(i);

            var titleTmp = GetTMP(item, ChildTitle);
            string slot = ClassifySlot(titleTmp != null ? titleTmp.text : null);
            if (slot == null) continue;   // 아침/점심/저녁 항목이 아니면 건너뜀

            var checkedTr = item.Find(ChildChecked);
            var img = checkedTr != null ? checkedTr.GetComponent<Image>() : null;
            var completeTmp = GetTMP(item, ChildComplete);

            var rec = BrushingStats.EarliestInSlotOnDate(records, slot, now);
            bool done = rec != null;

            if (img != null)
            {
                var sprite = done ? checkedSprite : uncheckedSprite;
                if (sprite != null) img.sprite = sprite;
            }

            if (completeTmp != null)
            {
                completeTmp.text = done
                    ? $"{BrushingStats.LocalTimeOf(rec):HH:mm} 완료"
                    : BrushingTimeSlot.ScheduleText(slot);
            }
        }
    }

    private static TMP_Text GetTMP(Transform parent, string childPath)
    {
        var t = parent.Find(childPath);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static string ClassifySlot(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        if (title.Contains(BrushingTimeSlot.Morning)) return BrushingTimeSlot.Morning;
        if (title.Contains(BrushingTimeSlot.Lunch))   return BrushingTimeSlot.Lunch;
        if (title.Contains(BrushingTimeSlot.Evening)) return BrushingTimeSlot.Evening;
        return null;
    }

    // ── 스코어 / 전주 대비 ────────────────────────────────────────

    private void RefreshScore(List<BrushingRecord> records, DateTime now)
    {
        DateTime thisMonday = BrushingStats.WeekStart(now);
        DateTime lastMonday = thisMonday.AddDays(-7);

        float thisAvg = BrushingStats.Average(records, thisMonday, now, out int thisCount);
        float lastAvg = BrushingStats.Average(records, lastMonday, thisMonday, out int lastCount);

        if (scoreText != null)
            scoreText.text = thisCount > 0 ? Mathf.RoundToInt(thisAvg).ToString() : "0";

        // 전주 기록이 없으면 비교 불가 → 변동 0(상승으로 취급).
        int delta = lastCount > 0 ? Mathf.RoundToInt(thisAvg) - Mathf.RoundToInt(lastAvg) : 0;
        bool declined = delta < 0;

        if (deltaText != null)
            deltaText.text = Mathf.Abs(delta).ToString();

        // 색상: 상승=캡처한 초록 유지, 하락=초록에서 색상만 빨강으로(채도·명도 유지).
        // 큰 평균 점수 숫자(scoreText)는 디자인상 검정이라 색을 바꾸지 않고 값만 갱신한다.
        // 초록 대상은 전주 대비 라벨(펜던트 Image)·변동량 Text·화살표 3가지.
        if (lastWeekScoreImage != null) lastWeekScoreImage.color = declined ? ToRed(_upPillColor) : _upPillColor;
        if (deltaText != null) deltaText.color = declined ? ToRed(_upDeltaColor) : _upDeltaColor;
        if (arrowImage != null) arrowImage.color = declined ? ToRed(_upArrowColor) : _upArrowColor;

        // 화살표 방향: 하락이면 Scale.Y를 음수로 뒤집는다(앵커가 달라 회전 대신 스케일로 반전).
        if (arrowImage != null)
        {
            var s = _arrowScaleUp;
            s.y = declined ? -Mathf.Abs(s.y) : Mathf.Abs(s.y);
            arrowImage.rectTransform.localScale = s;
        }
    }

    /// <summary>초록 계열 색을 같은 채도·명도의 빨강 계열로 변환(Hue만 0으로).</summary>
    private static Color ToRed(Color green)
    {
        Color.RGBToHSV(green, out _, out float s, out float v);
        var red = Color.HSVToRGB(0f, s, v);
        red.a = green.a;
        return red;
    }

    // ── Alert report (가장 부족한 구역) ───────────────────────────

    /// <summary>
    /// 최근 기록에서 가장 부족한 구역을 찾아 Teeth location/report에 표시한다.
    /// 부족한 구역이 최근 연속으로 안 닦였으면 경고(원래 빨강 유지) + "N일 연속 부족…",
    /// 양호하면 초록 + 칭찬 메시지로 바꾼다. (텍스트 색은 그대로, 배경 pill·배지 색만 전환)
    /// </summary>
    private void RefreshAlert(List<BrushingRecord> records, DateTime now)
    {
        var alert = BrushingStats.WorstZone(records, now);
        bool doingWell = !alert.hasData || alert.deficientDays <= 0;

        if (doingWell)
        {
            if (alertZoneText != null) alertZoneText.text = "모든 구역 양호";
            if (alertReportText != null) alertReportText.text = "현재 잘하고 있어요. 이대로만 계속하면 좋을 것 같아요.";
            if (alertBackground != null) alertBackground.color = ToGreen(_alertBgColor);
            if (alertBadge != null) alertBadge.color = ToGreen(_alertBadgeColor);
        }
        else
        {
            string zone = (alert.zoneIndex >= 0 && alert.zoneIndex < ZoneNames.Length)
                ? ZoneNames[alert.zoneIndex] : "";
            if (alertZoneText != null) alertZoneText.text = zone;
            if (alertReportText != null) alertReportText.text = $"{alert.deficientDays}일 연속 부족, 신경 써 볼까요?";
            if (alertBackground != null) alertBackground.color = _alertBgColor;
            if (alertBadge != null) alertBadge.color = _alertBadgeColor;
        }
    }

    /// <summary>빨강/주황 계열 색을 같은 채도·명도의 초록 계열로 변환(Hue만 초록으로).</summary>
    private static Color ToGreen(Color warm)
    {
        Color.RGBToHSV(warm, out _, out float s, out float v);
        var green = Color.HSVToRGB(0.3333333f, s, v);
        green.a = warm.a;
        return green;
    }
}
