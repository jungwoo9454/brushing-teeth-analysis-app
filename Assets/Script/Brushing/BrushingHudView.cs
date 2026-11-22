using System.Text;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 양치 씬 상단 HUD를 ML 검출/루틴/대사 이벤트에 바인딩하는 뷰.
/// 씬에 이미 배치된 TMP/Image들을 인스펙터로 연결받아, 코드 생성 없이 기존 UI를 갱신한다.
///   - 양치 상태 라벨(양치 중/양치 하지 않는 중) / 현재 닦는 구역 / 타이머 / 진행바(Fill) / 캐릭터 대사창
///   양치 상태·현재 구역은 ML의 ZoneDetectedEvent(매 프레임)로,
///   타이머·진행바는 루틴 이벤트(RoutineStarted/Progress/Completed)로 갱신한다.
///
/// 진행바는 Image.type=Filled의 fillAmount(0~1)로 "전체 양치 진행도"를 표시한다.
/// (0 = 시작, 1 = 목표 시간(기본 3분) 도달. 양치를 멈추면 시간이 안 흘러 진행바도 멈춘다.)
/// 대사가 바뀔 때는 LeanTween으로 알파 0→1 페이드인을 준다.
/// </summary>
public class BrushingHudView : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 연결 — 씬의 기존 오브젝트를 드래그
    // ─────────────────────────────────────────────────────────────

    [Header("양치 상태 / 타이머")]
    [Tooltip("양치 중인지 여부를 표시할 TMP (Top/GameObject/양치 중). " +
             "양치 중이면 \"양치 중\", 아니면 \"양치 하지 않는 중\". ZoneDetectedEvent.isEngaged로 갱신.")]
    [SerializeField] private TMP_Text currentZoneLabel;

    [Tooltip("전체 경과/목표 시간을 표시할 TMP (Top/GameObject/Time).")]
    [SerializeField] private TMP_Text timeLabel;

    [Header("현재 닦는 구역")]
    [Tooltip("지금 닦고 있는 구역명을 표시할 TMP (Top/NextArea/NextArea). " +
             "닦고 있지 않으면 \"없음\". ZoneDetectedEvent.uiZones로 갱신.")]
    [SerializeField] private TMP_Text nextZoneLabel;

    [Tooltip("현재 구역 안내 묶음 (Top/NextArea). OnEnable에서 항상 보이도록 켠다. 비워도 됨.")]
    [SerializeField] private GameObject nextAreaGroup;

    [Header("진행바 (Image type=Filled)")]
    [Tooltip("전체 양치 진행도를 fillAmount로 표시할 Image (Progress/Fill). 0=시작, 1=목표 시간 도달.")]
    [SerializeField] private Image progressFill;

    [Header("캐릭터 대사창")]
    [Tooltip("대사를 표시할 TMP (Character Talkballoon/Text (TMP)).")]
    [SerializeField] private TMP_Text dialogueLabel;

    [Tooltip("대사창 묶음 (Character Talkballoon). 대사가 없을 때 숨기고 싶으면 연결. 비워도 됨.")]
    [SerializeField] private GameObject dialogueGroup;

    [Tooltip("대사가 바뀔 때 알파 0→1 페이드인에 걸리는 시간(초). 0이면 즉시 표시.")]
    [SerializeField, Min(0f)] private float dialogueFadeDuration = 0.35f;

    [Header("표시 옵션")]
    [Tooltip("타이머 표시에 쓸 경과 시간 색(리치텍스트). 예: 11151A")]
    [SerializeField] private string elapsedColorHex = "11151A";
    [SerializeField] private string totalColorHex = "888C92";

    [Header("양치 완료 콜백")]
    [Tooltip("양치가 모두 끝났을 때 호출된다. 인스펙터에서 결과 화면 전환 등 원하는 동작을 연결.")]
    [SerializeField] private UnityEvent onBrushingFinished;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태 — 실시간 라벨(양치 상태/현재 구역) 변화 감지용 캐시.
    // 매 프레임 오는 ZoneDetectedEvent에서 값이 바뀐 경우에만 TMP에 써서
    // 불필요한 메시 재생성/GC를 피한다.
    // ─────────────────────────────────────────────────────────────

    private const int NoneKey = -1;             // "없음"(미검출)을 나타내는 zone key.

    private bool _engagedShown;                 // 마지막으로 표시한 isEngaged.
    private bool _hasEngagedShown;              // _engagedShown이 유효한지(첫 갱신 가드).
    private int  _zoneKeyShown = int.MinValue;  // 마지막으로 표시한 구역 key.
    private float _routineTotalSeconds;         // 루틴 전체 목표 시간 — 종료 시 타이머를 가득 채워 표시하는 데 사용.

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus.Subscribe<RoutineStartedEvent>(OnRoutineStarted);
        EventBus.Subscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Subscribe<RoutineCompletedEvent>(OnRoutineCompleted);
        EventBus.Subscribe<CharacterDialogueEvent>(OnDialogue);
        // 실시간 양치 상태/현재 구역 — ML 파이프라인이 매 프레임 발행.
        EventBus.Subscribe<ZoneDetectedEvent>(OnZoneDetected);
        // 세션 종료(=결과 화면 전환 트리거) — 진행바를 가득 채워 끝맺는다.
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);

        // 현재 구역 표시는 항상 보이도록(루틴 시절 숨겨졌을 수 있어 명시적으로 켠다).
        if (nextAreaGroup != null) nextAreaGroup.SetActive(true);

        // 변화 감지 캐시 리셋 + 첫 이벤트 수신 전 기본 표시값.
        _hasEngagedShown = false;
        _zoneKeyShown = int.MinValue;
        ShowEngaged(false);
        ShowCurrentZone(null, NoneKey);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RoutineStartedEvent>(OnRoutineStarted);
        EventBus.Unsubscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Unsubscribe<RoutineCompletedEvent>(OnRoutineCompleted);
        EventBus.Unsubscribe<CharacterDialogueEvent>(OnDialogue);
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZoneDetected);
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
    }

    // ─────────────────────────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────────────────────────

    /// <summary>루틴 시작 → 타이머/진행바 초기화.</summary>
    private void OnRoutineStarted(RoutineStartedEvent e)
    {
        _routineTotalSeconds = e.totalSeconds;
        SetFill(0f);
        SetTime(0f, e.totalSeconds);
    }

    /// <summary>
    /// 세션 종료(결과 화면으로 넘어가는 실제 트리거) → 진행바를 가득 채워 마무리한다.
    /// 진행바는 '양치한 시간(engaged)' 기준이라, 벽시계로 끝나는 세션 종료 시점엔 늘 약간 모자라
    /// 덜 찬 채로 화면이 넘어가 보였다 — 종료 순간 100%로 맞춰준다.
    /// (EventBus는 동기 발행이라 이 프레임에 채워진 뒤 다음 프레임에 씬이 전환된다.)
    /// </summary>
    private void OnSessionCompleted(BrushingSessionCompletedEvent e)
    {
        SetFill(1f);
        float total = _routineTotalSeconds > 0f ? _routineTotalSeconds : e.targetTotal;
        SetTime(total, total);
    }

    /// <summary>
    /// 매 프레임 ML 결과 → 실시간 라벨 2종 갱신.
    ///   - "양치 중" 라벨: isEngaged → "양치 중" / "양치 하지 않는 중"
    ///   - "현재 닦는 구역" 라벨: uiZones → 구역명(들) / "없음"
    /// 매 프레임 호출되므로 값이 바뀐 경우에만 TMP에 쓴다(ShowEngaged/ShowCurrentZone 내부 가드).
    /// </summary>
    private void OnZoneDetected(ZoneDetectedEvent e)
    {
        ShowEngaged(e.isEngaged);
        ShowCurrentZone(e.uiZones, ZoneKey(e.uiZones));
    }

    /// <summary>진행 중 → 전체 양치 진행도로 진행바 + 타이머 갱신.</summary>
    private void OnProgress(RoutineProgressEvent e)
    {
        _routineTotalSeconds = e.totalSeconds;
        // 진행바 = 전체 경과 / 전체 목표. 양치를 멈추면 totalElapsed가 안 늘어 진행바도 멈춘다.
        float ratio = e.totalSeconds > 0f ? e.totalElapsed / e.totalSeconds : 0f;
        SetFill(ratio);
        SetTime(e.totalElapsed, e.totalSeconds);
    }

    /// <summary>루틴 종료 → 진행바 가득 채우고 완료 콜백 발화.</summary>
    private void OnRoutineCompleted(RoutineCompletedEvent e)
    {
        SetFill(1f);
        SetTime(e.totalElapsed, e.totalSeconds);

        // 인스펙터 콜백 + 코드 훅 둘 다 호출 — 둘 중 편한 쪽으로 결과 처리.
        onBrushingFinished?.Invoke();
        HandleBrushingFinished(e);
    }

    /// <summary>대사 이벤트 → 대사창 텍스트 갱신 + 페이드인.</summary>
    private void OnDialogue(CharacterDialogueEvent e)
    {
        if (dialogueGroup != null) dialogueGroup.SetActive(true);
        if (dialogueLabel == null) return;

        dialogueLabel.text = e.text;
        PlayDialogueFade();
    }

    // ─────────────────────────────────────────────────────────────
    // 실시간 라벨 갱신 (변화가 있을 때만 TMP에 쓴다)
    // ─────────────────────────────────────────────────────────────

    /// <summary>"양치 중" 라벨 갱신 — 상태가 바뀐 경우에만.</summary>
    private void ShowEngaged(bool engaged)
    {
        if (currentZoneLabel == null) return;
        if (_hasEngagedShown && _engagedShown == engaged) return;
        _engagedShown = engaged;
        _hasEngagedShown = true;
        currentZoneLabel.text = engaged ? "양치 중" : "양치 하지 않는 중";
    }

    /// <summary>"현재 닦는 구역" 라벨 갱신 — key가 바뀐 경우에만 문자열을 새로 만든다.</summary>
    private void ShowCurrentZone(UiZone[] zones, int zoneKey)
    {
        if (nextZoneLabel == null) return;
        if (_zoneKeyShown == zoneKey) return;
        _zoneKeyShown = zoneKey;
        nextZoneLabel.text = (zoneKey == NoneKey) ? "없음" : DescribeZones(zones);
    }

    /// <summary>uiZones를 변화 감지용 정수 key로 압축 (없음=-1). 매 프레임 호출되므로 무할당.</summary>
    private static int ZoneKey(UiZone[] zones)
    {
        if (zones == null || zones.Length == 0) return NoneKey;
        int key = 0;
        for (int i = 0; i < zones.Length; i++)
            key = key * 8 + ((int)zones[i] + 1);
        return key;
    }

    /// <summary>uiZones를 "왼쪽 윗니, 왼쪽 아랫니" 같은 한글 문자열로. (key가 바뀔 때만 호출)</summary>
    private static string DescribeZones(UiZone[] zones)
    {
        if (zones == null || zones.Length == 0) return "없음";
        if (zones.Length == 1) return ZoneLabels.Korean(zones[0]);

        var sb = new StringBuilder();
        for (int i = 0; i < zones.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ZoneLabels.Korean(zones[i]));
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    // 사용자 확장 지점
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 양치가 모두 끝났을 때 호출되는 코드 훅.
    /// 결과 화면 전환·점수 저장 등 마무리 로직을 여기에 채워 넣으면 된다.
    /// (인스펙터 onBrushingFinished 콜백과 별개로, 코드로 다루고 싶을 때 사용)
    /// </summary>
    private void HandleBrushingFinished(RoutineCompletedEvent e)
    {
        // TODO: 양치 종료 후 동작 구현
        //   예) GameMng.ins.ChangeState(GameMng.GameState.Result);
        //   e.allStepsReachedTarget 로 성공/미완료 분기, e.totalElapsed 로 소요 시간 활용.
        Debug.Log($"[HUD] 양치 완료 — total={e.totalElapsed:F1}s, allReached={e.allStepsReachedTarget}");
    }

    // ─────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────

    /// <summary>대사창 텍스트 알파를 0→1로 페이드인 (LeanTween).</summary>
    private void PlayDialogueFade()
    {
        if (dialogueFadeDuration <= 0f)
        {
            dialogueLabel.alpha = 1f;
            return;
        }

        // 이전 페이드가 진행 중이면 취소하고 처음부터 — 대사가 연달아 바뀌어도 깔끔하게.
        LeanTween.cancel(dialogueLabel.gameObject);
        dialogueLabel.alpha = 0f;
        // TMP_Text는 UGUI Text가 아니라서 textAlpha 대신 value 콜백으로 alpha를 직접 보간한다.
        LeanTween.value(dialogueLabel.gameObject, a => dialogueLabel.alpha = a, 0f, 1f, dialogueFadeDuration)
                 .setEase(LeanTweenType.easeOutSine)
                 .setIgnoreTimeScale(true);
    }

    /// <summary>진행바 fillAmount 설정 (0~1 클램프).</summary>
    private void SetFill(float ratio)
    {
        if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(ratio);
    }

    /// <summary>"0:48 / 3:00" 형태로 타이머 표시 (씬 기존 리치텍스트 포맷 유지).</summary>
    private void SetTime(float elapsed, float total)
    {
        if (timeLabel == null) return;
        timeLabel.text =
            $"<b><color=#{elapsedColorHex}>{Mmss(elapsed)}</color></b>" +
            $"<color=#{totalColorHex}><size=80%> / {Mmss(total)}</size></color>";
    }

    /// <summary>초 → "m:ss" 문자열.</summary>
    private static string Mmss(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }
}
