using System.Text;

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 양치 ML 파이프라인의 실시간 상태를 화면 좌상단에 텍스트로 표시하는 상태 오버레이.
/// EventBus의 ZoneDetectedEvent를 구독해 매 프레임 다음 3가지를 갱신한다.
///   1) 칫솔이 닿은 5-zone 구역 (왼쪽/오른쪽 윗니·아랫니, 앞니)
///   2) 현재 양치 중인지 여부 (isEngaged)
///   3) 얼굴 인식(MediaPipe)이 잘 되는지 여부 (hasFace)
/// 추가로 BrushingSessionTickEvent를 구독해 5개 구역별 누적 양치 시간(초/비율)도 함께 표시한다.
///
/// UI는 코드로 직접 생성한다 — 전용 ScreenSpaceOverlay Canvas + Text 줄들.
/// 프리팹/씬 배치가 필요 없어, 이 컴포넌트 하나만 씬에 두면 바로 동작한다.
/// </summary>
public class BrushingStatusView : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 노출 필드
    // ─────────────────────────────────────────────────────────────

    [Header("Debug Overlay")]
    [Tooltip("켜면 좌상단에 ML 상태(현재 구역/양치 상태/얼굴 인식)와 구역별 누적 시간 디버그 오버레이를 표시한다. " +
             "끄면 UI를 만들지 않고 이벤트도 구독하지 않아 화면에 아무것도 그리지 않는다. " +
             "MediaPipe/ML 검증용 — 평소에는 꺼두는 것을 권장.")]
    [SerializeField] private bool showOverlay = true;

    [Header("Font")]
    [Tooltip("한글 표시용 Font. 비워두면 OS 한글 폰트(Malgun Gothic)로 자동 폴백한다.")]
    [SerializeField] private Font font;

    [Header("Layout")]
    [Tooltip("텍스트 크기 (px).")]
    [SerializeField] private int fontSize = 28;

    [Tooltip("화면 좌상단으로부터의 여백 (px).")]
    [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);

    // ─────────────────────────────────────────────────────────────
    // 내부 상태 — BuildUI에서 생성한 갱신 대상 Text.
    // ─────────────────────────────────────────────────────────────

    private Text _zoneText;
    private Text _brushingText;
    private Text _faceText;

    // 구역별 누적 시간 표시 — 헤더 1줄 + UiZone 개수만큼의 줄(인덱스 = (int)UiZone).
    private Text _zoneTimeHeader;
    private Text[] _zoneTimeLines;

    // 부울 상태를 한눈에 구분하기 위한 색상.
    private static readonly Color OkColor   = new Color(0.45f, 0.92f, 0.45f);  // 정상/양치 중
    private static readonly Color IdleColor = new Color(0.85f, 0.85f, 0.85f);  // 대기
    private static readonly Color BadColor  = new Color(0.96f, 0.50f, 0.40f);  // 인식 실패

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // 오버레이를 끄면 UI 생성과 이벤트 구독을 모두 생략한다.
        // enabled = false 로 두면 이후 OnEnable이 호출되지 않아 이벤트 구독도 일어나지 않는다.
        if (!showOverlay)
        {
            enabled = false;
            return;
        }

        BuildUI();
    }

    // BuildUI를 거치지 않은(showOverlay=false) 상태에서 OnEnable이 호출되면
    // _zoneText 등이 null이라 핸들러에서 NRE가 난다. _zoneText 생성 여부로 가드한다.
    private void OnEnable()
    {
        if (_zoneText == null) return;
        EventBus.Subscribe<ZoneDetectedEvent>(OnZone);
        EventBus.Subscribe<BrushingSessionTickEvent>(OnSessionTick);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZone);
        EventBus.Unsubscribe<BrushingSessionTickEvent>(OnSessionTick);
    }

    // ─────────────────────────────────────────────────────────────
    // 이벤트 처리
    // ─────────────────────────────────────────────────────────────

    /// <summary>ZoneDetectedEvent 수신 → 3개 상태 Text 갱신.</summary>
    private void OnZone(ZoneDetectedEvent e)
    {
        // (1) 현재 구역 — uiZones(5-zone 매핑)를 한글 라벨로. 비어 있으면 미검출.
        _zoneText.text = "현재 구역: " + DescribeZones(e.uiZones);

        // (2) 양치 중 여부 — isEngaged: 칫솔이 입에 닿았는지.
        _brushingText.text = "양치 상태: " + (e.isEngaged ? "양치 중" : "대기 중");
        _brushingText.color = e.isEngaged ? OkColor : IdleColor;

        // (3) 얼굴 인식 여부 — hasFace: MediaPipe 얼굴 검출 성공 여부.
        _faceText.text = "얼굴 인식: " + (e.hasFace ? "정상" : "안 됨");
        _faceText.color = e.hasFace ? OkColor : BadColor;
    }

    /// <summary>
    /// BrushingSessionTickEvent 수신 → 구역별 누적 양치 시간/비율 갱신.
    /// zoneElapsed는 인덱스 = (int)UiZone, 길이 = UiZone 개수(5)인 누적 시간(초) 배열이다.
    /// </summary>
    private void OnSessionTick(BrushingSessionTickEvent e)
    {
        if (_zoneTimeLines == null) return;

        float total = e.totalElapsed;
        _zoneTimeHeader.text = $"구역별 누적 (전체 {total:F0}초)";

        var zones = e.zoneElapsed;
        for (int i = 0; i < _zoneTimeLines.Length; i++)
        {
            float sec = (zones != null && i < zones.Length) ? zones[i] : 0f;
            float pct = total > 0f ? sec / total * 100f : 0f;
            _zoneTimeLines[i].text = $"{ZoneLabels.Korean((UiZone)i)}: {sec:F1}초 ({pct:F0}%)";
        }
    }

    /// <summary>
    /// UiZone 배열을 한글 구역명 문자열로 변환.
    /// SideLeft/SideRight 검출은 파이프라인에서 2개 zone으로 분기되어 들어오므로 둘 다 표기된다.
    /// </summary>
    private static string DescribeZones(UiZone[] zones)
    {
        if (zones == null || zones.Length == 0) return "— (감지 안 됨)";

        var sb = new StringBuilder();
        for (int i = 0; i < zones.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ZoneLabels.Korean(zones[i]));
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    // UI 생성
    // ─────────────────────────────────────────────────────────────

    /// <summary>전용 Canvas + 반투명 패널 + Text 줄들을 코드로 생성한다.</summary>
    private void BuildUI()
    {
        var resolvedFont = ResolveFont();

        int zoneCount = System.Enum.GetValues(typeof(UiZone)).Length;
        // 상태 3줄 + 구역별 헤더 1줄 + 구역별 누적 zoneCount줄.
        int lineCount = 3 + 1 + zoneCount;

        const float pad = 16f;
        float lineHeight = fontSize + 12f;
        float panelWidth = 600f;
        float panelHeight = pad * 2f + lineHeight * lineCount;

        // 전용 오버레이 Canvas — 카메라 프리뷰 Canvas 위에 그리도록 sortingOrder를 높게.
        var canvasGo = new GameObject("BrushingStatusCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // 표시 전용이라 입력을 받지 않음 → GraphicRaycaster 불필요.

        // 가독성용 반투명 배경 패널 — 좌상단 고정.
        var panel = CreateRect("StatusPanel", canvasGo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(screenMargin.x, -screenMargin.y);
        panel.sizeDelta = new Vector2(panelWidth, panelHeight);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        // 상태 3줄 Text — 패널 안에서 위→아래로 배치.
        _zoneText     = CreateLine(panel, resolvedFont, 0, pad, lineHeight, panelWidth);
        _brushingText = CreateLine(panel, resolvedFont, 1, pad, lineHeight, panelWidth);
        _faceText     = CreateLine(panel, resolvedFont, 2, pad, lineHeight, panelWidth);

        // 구역별 누적 시간 — 헤더 1줄 + zoneCount줄(상태 3줄 아래에 이어서 배치).
        _zoneTimeHeader = CreateLine(panel, resolvedFont, 3, pad, lineHeight, panelWidth);
        _zoneTimeLines = new Text[zoneCount];
        for (int i = 0; i < zoneCount; i++)
            _zoneTimeLines[i] = CreateLine(panel, resolvedFont, 4 + i, pad, lineHeight, panelWidth);

        // 첫 이벤트 수신 전 초기 표시값.
        _zoneText.text = "현재 구역: — (감지 안 됨)";
        _brushingText.text = "양치 상태: 대기 중";
        _brushingText.color = IdleColor;
        _faceText.text = "얼굴 인식: 안 됨";
        _faceText.color = BadColor;

        _zoneTimeHeader.text = "구역별 누적 (전체 0초)";
        for (int i = 0; i < zoneCount; i++)
            _zoneTimeLines[i].text = $"{ZoneLabels.Korean((UiZone)i)}: 0.0초 (0%)";
    }

    /// <summary>패널 안에 좌상단 기준으로 한 줄짜리 Text를 만든다.</summary>
    private Text CreateLine(RectTransform panel, Font lineFont, int index,
                            float pad, float lineHeight, float panelWidth)
    {
        var rt = CreateRect("Line" + index, panel);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(panelWidth - pad * 2f, lineHeight);
        rt.anchoredPosition = new Vector2(pad, -(pad + index * lineHeight));

        var text = rt.gameObject.AddComponent<Text>();
        text.font = lineFont;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        // 긴 구역명이 잘리지 않도록 가로 넘침 허용.
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>RectTransform만 가진 빈 UI GameObject를 만들어 parent 아래에 붙인다.</summary>
    private static RectTransform CreateRect(string objName, Transform parent)
    {
        var go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>
    /// 표시에 쓸 Font를 결정한다. 인스펙터에 지정됐으면 그것을, 아니면 OS 한글 폰트로 폴백.
    /// (legacy Text는 동적 폰트가 필요 — 기본 내장 폰트는 한글 글리프가 없다.)
    /// </summary>
    private Font ResolveFont()
    {
        if (font != null) return font;

        // Windows 기본 한글 폰트 — 별도 에셋 세팅 없이 한글 렌더 보장.
        var osFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, fontSize);
        if (osFont != null) return osFont;

        // 최후 폴백 — 한글은 깨질 수 있으나 최소한 동작은 한다.
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
