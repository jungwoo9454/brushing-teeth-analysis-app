using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 양치 씬 상단 HUD를 루틴/대사 이벤트에 바인딩하는 뷰.
/// 씬에 이미 배치된 TMP/Image들을 인스펙터로 연결받아, 코드 생성 없이 기존 UI를 갱신한다.
///   - 현재 구역 라벨 / 타이머 / 다음 구역 / 진행바(Fill) / 캐릭터 대사창
///
/// 진행바는 Image.type=Filled의 fillAmount(0~1)로 현재 단계 진행도를 표시한다.
/// (씬의 Progress/Fill 이 이미 Filled·Horizontal 로 세팅돼 있음)
/// </summary>
public class BrushingHudView : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 연결 — 씬의 기존 오브젝트를 드래그
    // ─────────────────────────────────────────────────────────────

    [Header("현재 구역 / 타이머")]
    [Tooltip("현재 닦는 구역명을 표시할 TMP (Top/GameObject/양치 중).")]
    [SerializeField] private TMP_Text currentZoneLabel;

    [Tooltip("전체 경과/목표 시간을 표시할 TMP (Top/GameObject/Time).")]
    [SerializeField] private TMP_Text timeLabel;

    [Header("다음 구역")]
    [Tooltip("다음 구역명을 표시할 TMP (Top/NextArea/NextArea).")]
    [SerializeField] private TMP_Text nextZoneLabel;

    [Tooltip("다음 구역 안내 묶음 (Top/NextArea). 마지막 단계에서 통째로 숨긴다. 비워도 됨.")]
    [SerializeField] private GameObject nextAreaGroup;

    [Header("진행바 (Image type=Filled)")]
    [Tooltip("현재 단계 진행도를 fillAmount로 표시할 Image (Progress/Fill).")]
    [SerializeField] private Image progressFill;

    [Header("캐릭터 대사창")]
    [Tooltip("대사를 표시할 TMP (Character Talkballoon/Text (TMP)).")]
    [SerializeField] private TMP_Text dialogueLabel;

    [Tooltip("대사창 묶음 (Character Talkballoon). 대사가 없을 때 숨기고 싶으면 연결. 비워도 됨.")]
    [SerializeField] private GameObject dialogueGroup;

    [Header("표시 옵션")]
    [Tooltip("타이머 표시에 쓸 전체 목표 시간 색(리치텍스트). 예: 11151A")]
    [SerializeField] private string elapsedColorHex = "11151A";
    [SerializeField] private string totalColorHex = "888C92";

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        EventBus.Subscribe<RoutineStartedEvent>(OnRoutineStarted);
        EventBus.Subscribe<RoutineStepStartedEvent>(OnStepStarted);
        EventBus.Subscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Subscribe<RoutineCompletedEvent>(OnRoutineCompleted);
        EventBus.Subscribe<CharacterDialogueEvent>(OnDialogue);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RoutineStartedEvent>(OnRoutineStarted);
        EventBus.Unsubscribe<RoutineStepStartedEvent>(OnStepStarted);
        EventBus.Unsubscribe<RoutineProgressEvent>(OnProgress);
        EventBus.Unsubscribe<RoutineCompletedEvent>(OnRoutineCompleted);
        EventBus.Unsubscribe<CharacterDialogueEvent>(OnDialogue);
    }

    // ─────────────────────────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────────────────────────

    /// <summary>루틴 시작 → 타이머/진행바 초기화.</summary>
    private void OnRoutineStarted(RoutineStartedEvent e)
    {
        SetFill(0f);
        SetTime(0f, e.totalSeconds);
    }

    /// <summary>단계 시작 → 현재/다음 구역 라벨 갱신, 진행바 리셋.</summary>
    private void OnStepStarted(RoutineStepStartedEvent e)
    {
        if (currentZoneLabel != null) currentZoneLabel.text = ZoneLabels.Korean(e.targetZone);

        // 다음 구역 안내 — 마지막 단계면 묶음을 숨긴다.
        if (nextAreaGroup != null) nextAreaGroup.SetActive(e.hasNext);
        if (nextZoneLabel != null && e.hasNext) nextZoneLabel.text = ZoneLabels.Korean(e.nextZone);

        SetFill(0f);
    }

    /// <summary>진행 중 → 진행바 fillAmount + 타이머 갱신.</summary>
    private void OnProgress(RoutineProgressEvent e)
    {
        float ratio = e.stepTargetSeconds > 0f ? e.stepElapsed / e.stepTargetSeconds : 0f;
        SetFill(ratio);
        SetTime(e.totalElapsed, e.totalSeconds);
    }

    /// <summary>루틴 종료 → 진행바 가득 채움.</summary>
    private void OnRoutineCompleted(RoutineCompletedEvent e)
    {
        SetFill(1f);
        SetTime(e.totalElapsed, e.totalSeconds);
    }

    /// <summary>대사 이벤트 → 대사창 텍스트 갱신.</summary>
    private void OnDialogue(CharacterDialogueEvent e)
    {
        if (dialogueGroup != null) dialogueGroup.SetActive(true);
        if (dialogueLabel != null) dialogueLabel.text = e.text;
    }

    // ─────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────

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
