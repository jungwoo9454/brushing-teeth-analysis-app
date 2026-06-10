using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 브러싱 디테일 리포트 화면의 데이터 바인딩 + 달력 연동. BrushingDetailReport 프리팹 루트(Canvas)에 붙인다.
///
/// 동작:
///   1) Top/Calander 버튼 → Calander(기간 선택) 프리팹을 팝업으로 띄움.
///      Calander의 Top/Back을 누르면(Closed 콜백) 선택 범위를 읽어 팝업을 닫고 리포트를 갱신.
///   2) Total score: 선택 기간 평균 점수를 Score 텍스트에, Progress/Fill의 fillAmount(가로)로 시각화.
///   3) Coordinate report: 기간 평균 좌우/상하 균형을 Pointer 위치 + LR/UB Progress(Left/Right fillAmount)로 표시.
///
/// 자식 참조는 경로로 자동 탐색하므로, 프리팹에는 이 컴포넌트와 'calendarPrefab'(Calander)만 지정하면 된다.
/// 균형 비율: 좌=왼쪽 어금니, 우=오른쪽, 상=윗니, 하=아랫니(앞니 제외) — BrushingStats.Period 참고.
/// (UB Progress의 Left=상(위), Right=하(아래)로 매핑한다.)
/// </summary>
[DisallowMultipleComponent]
public class BrushingDetailReportView : MonoBehaviour
{
    [Header("달력 팝업")]
    [Tooltip("기간 선택용 Calander 프리팹.")]
    [SerializeField] private GameObject calendarPrefab;
    [SerializeField] private Button calendarButton;   // Top/Calander

    [Header("참조 (비우면 경로로 자동 탐색)")]
    [SerializeField] private TMP_Text dateText;        // Top/Back/TextInfo/Date — 선택 기간
    [SerializeField] private TMP_Text scoreText;       // Total score/Score — 평균 점수
    [SerializeField] private Image scoreFill;          // Total score/Progress/Fill — 점수 바
    [SerializeField] private RectTransform pointer;    // Coordinate/Pointer
    [SerializeField] private RectTransform coordinate; // Coordinate (평면)
    [SerializeField] private Image lrLeft, lrRight;    // LR Progress/Left, Right
    [SerializeField] private Image ubLeft, ubRight;    // UB Progress/Left(상), Right(하)
    [SerializeField] private TMP_Text balanceStateText;// Coordinate report/Top/State/Text (TMP)

    private const string PathCalendarBtn = "RealSpace/Top/Calander";
    private const string PathDate    = "RealSpace/Top/Back/TextInfo/Date";
    private const string PathScore   = "RealSpace/Total score/Score";
    private const string PathFill    = "RealSpace/Total score/Progress/Fill";
    private const string PathPointer = "RealSpace/Coordinate report/Coordinate/Pointer";
    private const string PathCoord   = "RealSpace/Coordinate report/Coordinate";
    private const string PathLRLeft  = "RealSpace/Coordinate report/Progress/LR Progress/Left";
    private const string PathLRRight = "RealSpace/Coordinate report/Progress/LR Progress/Right";
    private const string PathUBLeft  = "RealSpace/Coordinate report/Progress/UB Progress/Left";
    private const string PathUBRight = "RealSpace/Coordinate report/Progress/UB Progress/Right";
    private const string PathState   = "RealSpace/Coordinate report/Top/State/Text (TMP)";

    private DateTime _from, _to;
    private bool _hasPeriod;
    private GameObject _calendarInstance;
    private bool _calendarOpen;   // 달력을 띄우려고 리포트를 잠시 끈 상태인지

    private void Awake()
    {
        ResolveRefs();
        if (calendarButton != null) calendarButton.onClick.AddListener(OpenCalendar);
    }

    private void OnEnable()
    {
        if (!_hasPeriod)
        {
            // 첫 표시 기본 기간 — 최근 7일.
            _to = DateTime.Today;
            _from = _to.AddDays(-6);
            _hasPeriod = true;
        }
        Refresh();
    }

    // 리포트가 '진짜로' 닫힐 때(탭 전환 등)만 달력 정리. 달력 표시용으로 잠시 끈 경우엔 유지.
    private void OnDisable() { if (!_calendarOpen) CloseCalendar(); }

    // 화면이 완전히 파괴(탭 전환 등)되면 열려 있던 달력도 같이 정리.
    private void OnDestroy() => CloseCalendar();

    // ── 참조 ──────────────────────────────────────────────────────

    private void ResolveRefs()
    {
        if (calendarButton == null) calendarButton = FindComp<Button>(PathCalendarBtn);
        if (dateText == null)  dateText  = FindComp<TMP_Text>(PathDate);
        if (scoreText == null) scoreText = FindComp<TMP_Text>(PathScore);
        if (scoreFill == null) scoreFill = FindComp<Image>(PathFill);
        if (pointer == null)    pointer    = FindRect(PathPointer);
        if (coordinate == null) coordinate = FindRect(PathCoord);
        if (lrLeft == null)  lrLeft  = FindComp<Image>(PathLRLeft);
        if (lrRight == null) lrRight = FindComp<Image>(PathLRRight);
        if (ubLeft == null)  ubLeft  = FindComp<Image>(PathUBLeft);
        if (ubRight == null) ubRight = FindComp<Image>(PathUBRight);
        if (balanceStateText == null) balanceStateText = FindComp<TMP_Text>(PathState);
    }

    private T FindComp<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    private RectTransform FindRect(string path)
    {
        var t = transform.Find(path);
        return t as RectTransform;
    }

    // ── 달력 팝업 ─────────────────────────────────────────────────

    private void OpenCalendar()
    {
        if (_calendarInstance != null) return;   // 이미 열려 있음
        if (calendarPrefab == null)
        {
            Debug.LogError("[BrushingDetailReportView] calendarPrefab이 연결되지 않았다 — 인스펙터에서 Calander 프리팹을 지정하라.");
            return;
        }

        _calendarInstance = Instantiate(calendarPrefab);

        // Overlay Canvas 정렬 순서만 정리(리포트가 꺼져 있어도 단독으로 보이도록).
        var canvas = _calendarInstance.GetComponent<Canvas>();
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = 100; }

        var picker = _calendarInstance.GetComponentInChildren<CalendarRangePicker>(true);
        if (picker == null)
        {
            Debug.LogError("[BrushingDetailReportView] Calander 프리팹에서 CalendarRangePicker를 찾지 못했다.");
            Destroy(_calendarInstance);
            _calendarInstance = null;
            return;
        }
        picker.Closed += OnCalendarClosed;

        // 직전 선택(첫 표시 때는 OnEnable의 기본 기간=최근 7일)을 달력에 복원 — 다시 열어도 그대로 보이게.
        if (_hasPeriod) picker.SetSelectedRange(_from, _to);

        // 달력이 열리는 동안 리포트 창은 잠시 끈다. (_calendarOpen을 먼저 켜서 OnDisable이 달력을 파괴하지 않게)
        _calendarOpen = true;
        gameObject.SetActive(false);
    }

    private void OnCalendarClosed(CalendarRangePicker picker)
    {
        // 시작만 있어도(단일 날짜) 그 하루를 기간으로 인정.
        if (picker.RangeStart.HasValue)
        {
            _from = picker.RangeStart.Value.Date;
            _to = (picker.RangeEnd ?? picker.RangeStart.Value).Date;
            _hasPeriod = true;
        }

        _calendarOpen = false;
        CloseCalendar();              // 달력 닫기(파괴)
        gameObject.SetActive(true);   // 리포트 창 다시 켜기 → OnEnable에서 Refresh로 갱신
    }

    private void CloseCalendar()
    {
        if (_calendarInstance == null) return;
        var picker = _calendarInstance.GetComponentInChildren<CalendarRangePicker>(true);
        if (picker != null) picker.Closed -= OnCalendarClosed;
        Destroy(_calendarInstance);
        _calendarInstance = null;
    }

    // ── 갱신 ──────────────────────────────────────────────────────

    private void Refresh()
    {
        var records = LoadRecords();
        var rep = BrushingStats.Period(records, _from, _to);

        if (dateText != null) dateText.text = FormatRange(_from, _to);

        // 통합 점수 + 진행 바.
        if (scoreText != null) scoreText.text = Mathf.RoundToInt(rep.avgScore).ToString();
        if (scoreFill != null)
        {
            scoreFill.type = Image.Type.Filled;
            scoreFill.fillMethod = Image.FillMethod.Horizontal;
            scoreFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            scoreFill.fillAmount = Mathf.Clamp01(rep.avgScore / 100f);
        }

        // 좌우/상하 균형 바 (Left/Right fillAmount, 합=1이면 가운데서 만남).
        if (lrLeft != null)  lrLeft.fillAmount  = rep.leftRatio;
        if (lrRight != null) lrRight.fillAmount = rep.rightRatio;
        if (ubLeft != null)  ubLeft.fillAmount  = rep.upRatio;
        if (ubRight != null) ubRight.fillAmount = rep.downRatio;

        UpdatePointer(rep);
        UpdateBalanceState(rep);
    }

    private void UpdatePointer(PeriodReport rep)
    {
        if (pointer == null || coordinate == null) return;

        // Coordinate 중앙 기준 가동 범위(포인터가 평면 밖으로 안 나가도록).
        float maxX = Mathf.Max(0f, (coordinate.rect.width  - pointer.rect.width)  / 2f);
        float maxY = Mathf.Max(0f, (coordinate.rect.height - pointer.rect.height) / 2f);

        // 왼쪽을 더 닦았으면 왼쪽(-x), 윗니를 더 닦았으면 위(+y)로.
        float x = (rep.rightRatio - rep.leftRatio) * maxX;
        float y = (rep.upRatio - rep.downRatio) * maxY;
        pointer.anchoredPosition = new Vector2(x, y);
    }

    private void UpdateBalanceState(PeriodReport rep)
    {
        if (balanceStateText == null) return;
        if (rep.count == 0) { balanceStateText.text = "기록 없음"; return; }

        float lrDev = Mathf.Abs(rep.leftRatio - 0.5f);
        float udDev = Mathf.Abs(rep.upRatio - 0.5f);
        balanceStateText.text = (lrDev < 0.1f && udDev < 0.1f) ? "균형 좋음" : "불균형";
    }

    private List<BrushingRecord> LoadRecords()
    {
        try { return new JsonBrushingRecordRepository().LoadAll(); }
        catch { return new List<BrushingRecord>(); }
    }

    private static string FormatRange(DateTime from, DateTime to)
    {
        if (from.Date == to.Date) return $"{from.Month}월 {from.Day}일";
        return (from.Year == to.Year && from.Month == to.Month)
            ? $"{from.Month}월 {from.Day}일 - {to.Day}일"
            : $"{from.Month}월 {from.Day}일 - {to.Month}월 {to.Day}일";
    }
}
