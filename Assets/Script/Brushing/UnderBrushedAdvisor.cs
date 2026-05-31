using UnityEngine;

/// <summary>
/// 5구역 누적 양치 시간을 비교해, 상대적으로 덜 닦인 구역을 찾아 "더 닦아주세요" 안내를 띄우는 어드바이저.
/// BrushingSession이 발행하는 BrushingSessionTickEvent(구역별 누적 시간)을 구독하고,
/// 가장 많이 닦은 구역 대비 일정 비율 미만으로 닦인 구역이 있으면 CharacterDialogueEvent를 발행한다.
/// 말풍선 UI(BrushingHudView.OnDialogue)가 그 이벤트를 받아 Text를 갱신하므로, UI 코드는 손대지 않는다.
///
/// 잔소리를 막기 위한 게이트:
///   - 양치 초반(minTotalBeforeAdvice 이전)에는 판정하지 않는다 — 데이터가 적어 한쪽으로 편향됨.
///   - 부족 구역이라도 이미 충분히(satisfiedSeconds) 닦였으면 안내하지 않는다.
///   - adviceCooldown 간격 이상으로만 안내한다.
///   - 지금 그 부족 구역을 닦고 있으면(이미 교정 중) 굳이 안내하지 않는다.
///
/// BrushingSession과 같은 GameObject(BrushingPipelineRig)에 함께 부착한다.
/// </summary>
public class UnderBrushedAdvisor : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 판정 기준
    // ─────────────────────────────────────────────────────────────

    [Header("판정 기준")]
    [Tooltip("전체 양치 시간이 이 값(초) 이상 쌓인 뒤부터 부족 구역 판정을 시작한다. 초반 편향 방지.")]
    [SerializeField, Min(0f)] private float minTotalBeforeAdvice = 20f;

    [Tooltip("가장 많이 닦은 구역 대비 이 비율 미만으로 닦인 구역을 '부족'으로 본다. 0.5 = 절반 미만.")]
    [SerializeField, Range(0.05f, 1f)] private float deficitRatio = 0.5f;

    [Tooltip("부족 구역의 절대 누적 시간이 이 값(초) 이상이면 안내하지 않는다 — 이미 충분히 닦음.")]
    [SerializeField, Min(0f)] private float satisfiedSeconds = 24f;

    [Header("안내 빈도")]
    [Tooltip("부족 구역 안내를 다시 띄우기까지의 최소 간격(초). 잔소리 방지.")]
    [SerializeField, Min(0f)] private float adviceCooldown = 12f;

    // ─────────────────────────────────────────────────────────────
    // 멘트
    // ─────────────────────────────────────────────────────────────

    [Header("멘트 — \"{zone}\"은 구역명으로 치환")]
    [Tooltip("부족 구역 안내 문구. 예: \"{zone}가 아직 부족해요! 더 닦아주세요\"")]
    [SerializeField] private string adviceTemplate = "{zone}가 아직 부족해요! 더 닦아주세요";

    [Tooltip("켜면 발행되는 안내를 콘솔에도 찍는다 — 검증용.")]
    [SerializeField] private bool logEvents = false;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // 마지막 안내 시각 — adviceCooldown throttle용.
    private float lastAdviceTime = -999f;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()  => EventBus.Subscribe<BrushingSessionTickEvent>(OnTick);
    private void OnDisable() => EventBus.Unsubscribe<BrushingSessionTickEvent>(OnTick);

    // ─────────────────────────────────────────────────────────────
    // 판정
    // ─────────────────────────────────────────────────────────────

    /// <summary>세션 Tick마다 부족 구역을 검사하고, 조건을 만족하면 안내를 발행한다.</summary>
    private void OnTick(BrushingSessionTickEvent e)
    {
        // 초반/쿨다운 게이트.
        if (e.totalElapsed < minTotalBeforeAdvice) return;
        if (Time.time - lastAdviceTime < adviceCooldown) return;

        var zones = e.zoneElapsed;
        if (zones == null || zones.Length == 0) return;

        // 최소/최대 구역 찾기.
        int minIdx = 0;
        float minVal = zones[0];
        float maxVal = zones[0];
        for (int i = 1; i < zones.Length; i++)
        {
            if (zones[i] < minVal) { minVal = zones[i]; minIdx = i; }
            if (zones[i] > maxVal) { maxVal = zones[i]; }
        }

        // 비교 기준이 없거나(아직 아무 데도 안 닦음), 부족 구역도 이미 충분하면 안내 불필요.
        if (maxVal <= 0f) return;
        if (minVal >= satisfiedSeconds) return;
        if (minVal >= maxVal * deficitRatio) return;

        var deficitZone = (UiZone)minIdx;

        // 지금 바로 그 부족 구역을 닦고 있으면(이미 교정 중) 굳이 잔소리하지 않는다.
        if (e.isEngaged && IsBrushing(e.currentZone, deficitZone)) return;

        lastAdviceTime = Time.time;
        string text = adviceTemplate.Replace("{zone}", ZoneLabels.Korean(deficitZone));

        if (logEvents)
            Debug.Log($"[UnderBrushed] {text} (min={minVal:F1}s, max={maxVal:F1}s, zone={deficitZone})");

        EventBus.Publish(new CharacterDialogueEvent { text = text, kind = DialogueKind.Encourage });
    }

    /// <summary>현재 닦는 ML 구역(currentZone)이 target UI 구역을 포함하는지.</summary>
    private static bool IsBrushing(MlZone currentZone, UiZone target)
    {
        var uiZones = UIZoneMapper.Map(currentZone);
        for (int i = 0; i < uiZones.Length; i++)
            if (uiZones[i] == target) return true;
        return false;
    }
}
