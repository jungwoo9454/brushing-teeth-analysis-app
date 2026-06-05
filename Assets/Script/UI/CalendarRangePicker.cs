using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 달력 기반 날짜 "범위" 선택 위젯. (다른 앱의 기간 선택 UI와 동일한 동작)
///
/// 동작:
///   - Month Select의 Prev/Next 버튼으로 표시 월을 전환한다. (Month 라벨에 "yyyy년 M월")
///   - Day Calander(7열 GridLayoutGroup)에 해당 월의 1일~말일을 요일(Sun~Sat)에 맞춰 채운다.
///   - 각 칸(Slot)의 Background/State는 평소 비활성. 날짜를 클릭하면:
///       · 첫 클릭        → 그 날짜가 시작점. Background 활성(끝점 스프라이트 = 기존 Background 이미지).
///       · 두 번째 클릭   → 시작~끝 "사이" 날짜의 Background는 범위 스프라이트로, 양 끝점은 끝점 스프라이트로.
///       · 범위가 잡힌 상태에서 다시 클릭하면 새 선택을 시작한다.
///   - 선택 범위는 "Select date range" 텍스트에, 총 일수는 "Total day"의 Text(TMP)에 표시한다.
///
/// 칸 클릭 감지/리스너 연결, 자식(Background/State/Text) 참조는 모두 런타임에 구성하므로
/// 프리팹에는 이 컴포넌트만 붙이면 된다. (칸이 부족한 6주짜리 달은 첫 칸을 복제해 늘린다)
/// 클릭이 동작하려면 씬에 EventSystem이 있어야 한다(UGUI 공통 전제).
/// </summary>
[DisallowMultipleComponent]
public class CalendarRangePicker : MonoBehaviour
{
    [Header("스프라이트")]
    [Tooltip("선택 범위 '사이' 날짜 칸 Background에 쓰는 스프라이트.")]
    [SerializeField] private Sprite rangeSprite;
    [Tooltip("시작/끝 '끝점' 날짜 칸 Background 스프라이트. 비우면 칸 Background의 기존 스프라이트를 사용한다.")]
    [SerializeField] private Sprite endpointSprite;

    [Header("미래 날짜")]
    [Tooltip("오늘 이후(선택 불가) 날짜의 숫자 색. 기본 #8B8F94")]
    [SerializeField] private Color disabledTextColor = new Color(0.545098f, 0.560784f, 0.580392f, 1f);

    [Header("참조 (비우면 경로로 자동 탐색)")]
    [SerializeField] private TMP_Text monthLabel;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RectTransform dayGrid;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text totalDayText;

    [Header("디버그(테스트용) — 검증 후 제거")]
    [SerializeField] private bool debugAutoSelect = false;
    [SerializeField] private int debugStartDay = 0;
    [SerializeField] private int debugEndDay = 0;

    // 자동 탐색 경로 (이 컴포넌트가 붙은 root 기준 상대 경로)
    private const string PathMonth = "RealSpace/Month Select/Month";
    private const string PathPrev  = "RealSpace/Month Select/Prev";
    private const string PathNext  = "RealSpace/Month Select/Next";
    private const string PathGrid  = "RealSpace/Calander/Day Calander";
    private const string PathRange = "RealSpace/Total day/Select date range";
    private const string PathTotal = "RealSpace/Total day/Select date range/Total day/Text (TMP)";

    // Slot 내부 자식 이름
    private const string ChildBackground = "Background";
    private const string ChildText       = "Text (TMP)";
    private const string ChildState      = "State";

    /// <summary>달력 한 칸.</summary>
    private class DayCell
    {
        public GameObject root;
        public Button button;
        public GameObject background;
        public Image backgroundImage;
        public TMP_Text label;
        public GameObject state;
        public DateTime date;
        public bool isDay;   // 실제 날짜 칸인지(1일 앞쪽 빈칸 = false)
    }

    private readonly List<DayCell> _cells = new List<DayCell>();
    private Transform _slotTemplate;   // 칸 복제용 원본(첫 Slot)

    private int _year;
    private int _month;
    private DateTime? _start;
    private DateTime? _end;

    private Color _enabledTextColor = Color.white;  // 칸의 원래 숫자 색(프리팹에서 캡처)
    private bool _capturedTextColor;

    private void Awake()
    {
        ResolveReferences();
        BuildCells();

        if (prevButton != null) prevButton.onClick.AddListener(PrevMonth);
        if (nextButton != null) nextButton.onClick.AddListener(NextMonth);

        var today = DateTime.Today;
        _year = today.Year;
        _month = today.Month;

        ClearSelection();
        Render();

        // 디버그: 인스펙터에서 지정한 초기 범위를 자동 선택(클릭 없이 비주얼 확인용).
        if (debugAutoSelect && debugStartDay > 0 && debugEndDay > 0)
        {
            int max = DateTime.DaysInMonth(_year, _month);
            int s = Mathf.Clamp(Mathf.Min(debugStartDay, debugEndDay), 1, max);
            int e = Mathf.Clamp(Mathf.Max(debugStartDay, debugEndDay), 1, max);
            _start = new DateTime(_year, _month, s);
            _end = new DateTime(_year, _month, e);
            RefreshSelectionVisuals();
            RefreshSummary();
        }
    }

    // ── 참조/칸 구성 ───────────────────────────────────────────────

    private void ResolveReferences()
    {
        if (monthLabel == null)   monthLabel   = FindComp<TMP_Text>(PathMonth);
        if (rangeText == null)    rangeText    = FindComp<TMP_Text>(PathRange);
        if (totalDayText == null) totalDayText = FindComp<TMP_Text>(PathTotal);
        if (dayGrid == null)
        {
            var t = transform.Find(PathGrid);
            if (t != null) dayGrid = t as RectTransform;
        }
        if (prevButton == null) prevButton = ResolveButton(PathPrev);
        if (nextButton == null) nextButton = ResolveButton(PathNext);
    }

    private T FindComp<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    // Prev/Next는 Image만 있으므로 Button이 없으면 런타임에 추가한다.
    private Button ResolveButton(string path)
    {
        var t = transform.Find(path);
        if (t == null) return null;
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        return btn;
    }

    private void BuildCells()
    {
        _cells.Clear();
        if (dayGrid == null) return;

        for (int i = 0; i < dayGrid.childCount; i++)
        {
            var slot = dayGrid.GetChild(i);
            if (_slotTemplate == null) _slotTemplate = slot;
            _cells.Add(SetupCell(slot));
        }
    }

    /// <summary>Slot 하나를 클릭 가능한 DayCell로 구성한다. (여러 번 호출돼도 안전)</summary>
    private DayCell SetupCell(Transform slot)
    {
        var cell = new DayCell { root = slot.gameObject };

        // 칸 전체를 덮는 투명 클릭 영역(Image) + Button.
        var img = slot.GetComponent<Image>();
        if (img == null) img = slot.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);  // 투명(알파 0이어도 레이캐스트는 받음)
        img.raycastTarget = true;

        var btn = slot.GetComponent<Button>();
        if (btn == null) btn = slot.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;
        cell.button = btn;

        var bg = slot.Find(ChildBackground);
        if (bg != null)
        {
            cell.background = bg.gameObject;
            cell.backgroundImage = bg.GetComponent<Image>();
            if (cell.backgroundImage != null)
            {
                cell.backgroundImage.raycastTarget = false;
                // endpointSprite 미지정 시 칸 Background의 기존 스프라이트를 끝점 스프라이트로 사용.
                if (endpointSprite == null) endpointSprite = cell.backgroundImage.sprite;
            }
        }

        var txt = slot.Find(ChildText);
        if (txt != null)
        {
            cell.label = txt.GetComponent<TMP_Text>();
            if (cell.label != null)
            {
                cell.label.raycastTarget = false;
                // 프리팹의 원래 숫자 색을 1회 캡처 — 선택 가능한 날짜에 다시 적용한다.
                if (!_capturedTextColor) { _enabledTextColor = cell.label.color; _capturedTextColor = true; }
            }
        }

        var st = slot.Find(ChildState);
        if (st != null)
        {
            cell.state = st.gameObject;
            var stImg = st.GetComponent<Image>();
            if (stImg != null) stImg.raycastTarget = false;
        }

        btn.onClick.AddListener(() => OnDayClicked(cell));
        return cell;
    }

    private DayCell AddCell()
    {
        var clone = Instantiate(_slotTemplate.gameObject, dayGrid);
        clone.name = "Slot (auto)";
        var cell = SetupCell(clone.transform);
        _cells.Add(cell);
        return cell;
    }

    // ── 렌더링 ─────────────────────────────────────────────────────

    private void Render()
    {
        if (monthLabel != null) monthLabel.text = $"{_year}년 {_month}월";

        // 이번 달이면 더 이상 다음 달로 못 넘어가도록 Next 버튼 비활성.
        if (nextButton != null) nextButton.interactable = !IsAtOrAfterCurrentMonth();

        var first = new DateTime(_year, _month, 1);
        int offset = (int)first.DayOfWeek;          // 일=0 ... 토=6 (헤더 Sun~Sat과 일치)
        int days = DateTime.DaysInMonth(_year, _month);
        int needed = offset + days;

        while (_cells.Count < needed && _slotTemplate != null) AddCell();

        for (int i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (i < offset)
            {
                // 1일 앞의 빈칸 — 자리는 차지하되 클릭/표시 없음.
                cell.isDay = false;
                cell.root.SetActive(true);
                if (cell.label != null) cell.label.text = string.Empty;
                if (cell.button != null) cell.button.interactable = false;
                if (cell.background != null) cell.background.SetActive(false);
                if (cell.state != null) cell.state.SetActive(false);
            }
            else if (i < needed)
            {
                int dayNum = i - offset + 1;
                cell.isDay = true;
                cell.date = new DateTime(_year, _month, dayNum);
                bool selectable = cell.date <= DateTime.Today;   // 오늘 이후(미래)는 선택 불가
                cell.root.SetActive(true);
                if (cell.label != null)
                {
                    cell.label.text = dayNum.ToString();
                    cell.label.color = selectable ? _enabledTextColor : disabledTextColor;
                }
                if (cell.button != null) cell.button.interactable = selectable;
                if (cell.state != null) cell.state.SetActive(false);
                // Background는 RefreshSelectionVisuals에서 선택 상태에 따라 처리.
            }
            else
            {
                // 남는 칸은 통째로 비활성(빈 박스가 보이지 않도록).
                cell.isDay = false;
                cell.root.SetActive(false);
            }
        }

        RefreshSelectionVisuals();
        RefreshSummary();
    }

    // ── 선택 처리 ─────────────────────────────────────────────────

    private void OnDayClicked(DayCell cell)
    {
        if (!cell.isDay) return;
        if (cell.date > DateTime.Today) return;   // 오늘 이후(미래) 날짜는 선택 불가
        var date = cell.date;

        if (_start == null || _end != null)
        {
            // 새 선택 시작
            _start = date;
            _end = null;
        }
        else
        {
            // 두 번째 클릭으로 범위 확정 (앞뒤 자동 정렬)
            if (date < _start.Value) { _end = _start; _start = date; }
            else _end = date;
        }

        RefreshSelectionVisuals();
        RefreshSummary();
    }

    private void ClearSelection()
    {
        _start = null;
        _end = null;
    }

    /// <summary>현재 표시 중인 달의 칸들에 선택 상태(끝점/사이/비선택)를 반영.</summary>
    private void RefreshSelectionVisuals()
    {
        foreach (var cell in _cells)
        {
            if (!cell.isDay || cell.background == null) continue;

            bool inRange;
            bool isEndpoint;
            DateTime d = cell.date;

            if (_start == null)
            {
                inRange = false;
                isEndpoint = false;
            }
            else if (_end == null)
            {
                inRange = d == _start.Value;
                isEndpoint = inRange;
            }
            else
            {
                inRange = d >= _start.Value && d <= _end.Value;
                isEndpoint = d == _start.Value || d == _end.Value;
            }

            if (!inRange)
            {
                cell.background.SetActive(false);
            }
            else
            {
                cell.background.SetActive(true);
                if (cell.backgroundImage != null)
                    cell.backgroundImage.sprite = isEndpoint ? endpointSprite : rangeSprite;
            }
        }
    }

    /// <summary>"Select date range" / "Total day" 텍스트 갱신.</summary>
    private void RefreshSummary()
    {
        if (_start == null)
        {
            if (rangeText != null) rangeText.text = string.Empty;
            if (totalDayText != null) totalDayText.text = string.Empty;
            return;
        }

        if (_end == null)
        {
            if (rangeText != null) rangeText.text = $"{_start.Value.Month}월 {_start.Value.Day}일";
            if (totalDayText != null) totalDayText.text = "1일";
            return;
        }

        DateTime s = _start.Value;
        DateTime e = _end.Value;
        if (rangeText != null)
        {
            rangeText.text = (s.Year == e.Year && s.Month == e.Month)
                ? $"{s.Month}월 {s.Day}일 - {e.Day}일"
                : $"{s.Month}월 {s.Day}일 - {e.Month}월 {e.Day}일";
        }
        if (totalDayText != null)
        {
            int total = (e - s).Days + 1;
            totalDayText.text = $"{total}일";
        }
    }

    // ── 월 전환 ───────────────────────────────────────────────────

    public void PrevMonth()
    {
        var d = new DateTime(_year, _month, 1).AddMonths(-1);
        _year = d.Year;
        _month = d.Month;
        Render();
    }

    public void NextMonth()
    {
        if (IsAtOrAfterCurrentMonth()) return;   // 이번 달 이후로는 넘어가지 않는다.
        var d = new DateTime(_year, _month, 1).AddMonths(1);
        _year = d.Year;
        _month = d.Month;
        Render();
    }

    // 표시 중인 달이 '이번 달' 이상인지(이번 달이면 더 못 넘어감).
    private bool IsAtOrAfterCurrentMonth()
    {
        var t = DateTime.Today;
        return _year > t.Year || (_year == t.Year && _month >= t.Month);
    }
}
