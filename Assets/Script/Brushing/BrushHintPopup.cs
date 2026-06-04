using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "~~ 부분을 더 닦아주세요"(Encourage) 멘트가 나올 때, 해당 구역의 안내 이미지를
/// LeanTween으로 "뿅" 띄워 주는 팝업. CharacterDialogueEvent를 구독한다.
///
/// 동작 규칙:
///   - 트리거 성격(기본 Encourage)이고 구역 정보가 있을 때만 반응.
///   - 직전에 띄운 구역과 같으면 다시 띄우지 않는다(중복 노출 방지) — 구역이 "달라졌을 때만" 새로 띄움.
///   - 띄운 뒤 autoHideSeconds(기본 3초)가 지나면 자동으로 작아지며 사라진다.
///   - 전면(셀카) 프리뷰는 좌우가 거울처럼 반전돼 보이므로, swapLeftRight로 왼/오른쪽 구역
///     이미지를 서로 바꿔(앞니는 그대로) 화면과 일치시킨다.
///
/// ⚠ 이벤트 구독을 위해 '항상 활성'인 오브젝트(예: Camera)에 붙인다.
///    비활성인 Example 자체에 붙이면 OnEnable이 실행되지 않아 구독되지 않는다.
/// </summary>
public class BrushHintPopup : MonoBehaviour
{
    [Header("팝업 대상")]
    [Tooltip("뿅 나타날 오브젝트. 비우면 이 오브젝트의 'Example' 자식을 자동으로 찾는다.")]
    [SerializeField] private GameObject popupObject;

    [Tooltip("구역 이미지를 표시할 Image. 비우면 popupObject의 Image를 사용.")]
    [SerializeField] private Image hintImage;

    [Header("구역별 안내 이미지 (인덱스 = UiZone: 0 왼쪽윗니, 1 오른쪽윗니, 2 왼쪽아랫니, 3 오른쪽아랫니, 4 앞니)")]
    [Tooltip("추후 채워 넣기. 비어 있는 구역은 기존 Image 스프라이트를 그대로 둔다.")]
    [SerializeField] private Sprite[] zoneSprites = new Sprite[5];

    [Header("트리거")]
    [Tooltip("이 성격의 멘트가 나올 때 팝업을 띄운다. 기본 Encourage(\"~ 더 닦아주세요\").")]
    [SerializeField] private DialogueKind triggerKind = DialogueKind.Encourage;

    [Tooltip("전면(셀카) 프리뷰는 좌우가 거울처럼 보이므로 왼/오른쪽 구역 이미지를 서로 바꿔 화면과 맞춘다. " +
             "이미지가 좌우 반대로 보이면 이 값을 토글해 맞추세요.")]
    [SerializeField] private bool swapLeftRight = true;

    [Header("뿅 연출 (LeanTween)")]
    [Tooltip("시작 스케일(0~1). 0이면 점에서부터 커진다.")]
    [SerializeField, Range(0f, 1f)] private float startScale = 0f;

    [Tooltip("커지는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.01f)] private float popDuration = 0.4f;

    [Tooltip("튀어나오는 이징. easeOutBack이 '뿅' 느낌.")]
    [SerializeField] private LeanTweenType popEase = LeanTweenType.easeOutBack;

    [Tooltip("이 시간(초) 뒤 자동으로 작아지며 사라진다. 0이면 다음 트리거/Hide 전까지 유지.")]
    [SerializeField, Min(0f)] private float autoHideSeconds = 3f;

    [Tooltip("사라질 때 작아지는 데 걸리는 시간(초).")]
    [SerializeField, Min(0.01f)] private float hideDuration = 0.25f;

    // 직전에 '띄운' 구역 — 같은 구역 연속 요청을 무시(중복 노출 방지)하기 위한 캐시.
    private UiZone _lastShownZone;
    private bool _hasShown;

    private void Awake()
    {
        // popupObject 미지정 시 'Example' 자식을 자동 탐색(비활성 자식도 Find로 잡힘).
        if (popupObject == null)
        {
            var child = transform.Find("Example");
            if (child != null) popupObject = child.gameObject;
        }
        if (hintImage == null && popupObject != null)
            hintImage = popupObject.GetComponent<Image>();

        // 시작 시 숨김 — 트리거 전까지 보이지 않게.
        if (popupObject != null) popupObject.SetActive(false);
    }

    private void OnEnable()  => EventBus.Subscribe<CharacterDialogueEvent>(OnDialogue);
    private void OnDisable() => EventBus.Unsubscribe<CharacterDialogueEvent>(OnDialogue);

    /// <summary>대사 수신 → 트리거 성격 + 구역 정보가 있고, 직전과 다른 구역일 때만 띄운다.</summary>
    private void OnDialogue(CharacterDialogueEvent e)
    {
        if (e.kind != triggerKind || !e.hasZone) return;

        // 중복 노출 방지 — 직전에 띄운 구역과 같으면 새로 띄우지 않는다(구역이 달라졌을 때만).
        if (_hasShown && e.zone == _lastShownZone) return;

        Show(e.zone);
    }

    /// <summary>지정 구역 이미지를 "뿅" 띄운다. (외부 직접 호출도 가능 — 중복 방지 캐시도 갱신)</summary>
    public void Show(UiZone zone)
    {
        if (popupObject == null) return;

        _lastShownZone = zone;
        _hasShown = true;

        // 전면 프리뷰 거울 반전 보정 — 왼/오른쪽 구역 이미지를 서로 바꿔 화면과 맞춘다.
        UiZone displayZone = swapLeftRight ? MirrorLeftRight(zone) : zone;

        // 등록된 스프라이트가 있을 때만 교체 — 없으면 기존 이미지 유지(이미지는 추후 추가).
        int idx = (int)displayZone;
        if (hintImage != null && zoneSprites != null &&
            idx >= 0 && idx < zoneSprites.Length && zoneSprites[idx] != null)
        {
            hintImage.sprite = zoneSprites[idx];
        }

        popupObject.SetActive(true);

        // 진행 중 트윈(이전 팝업의 등장/자동숨김)을 모두 취소하고 새로 시작.
        LeanTween.cancel(popupObject);
        popupObject.transform.localScale = Vector3.one * startScale;

        // 뿅: 작은 스케일 → 1로 easeOutBack. 양치 중 timeScale=0이어도 동작하도록 ignoreTimeScale.
        LeanTween.scale(popupObject, Vector3.one, popDuration)
                 .setEase(popEase)
                 .setIgnoreTimeScale(true);

        // autoHideSeconds 뒤 자동으로 작아지며 사라짐.
        if (autoHideSeconds > 0f)
        {
            LeanTween.scale(popupObject, Vector3.one * startScale, hideDuration)
                     .setDelay(autoHideSeconds)
                     .setEase(LeanTweenType.easeInBack)
                     .setIgnoreTimeScale(true)
                     .setOnComplete(HideImmediate);
        }
    }

    /// <summary>즉시 숨김(진행 중 트윈 취소 + 비활성).</summary>
    public void Hide()
    {
        if (popupObject == null) return;
        LeanTween.cancel(popupObject);
        HideImmediate();
    }

    /// <summary>중복 방지 캐시 초기화 — 같은 구역이라도 다음 트리거에 다시 띄우고 싶을 때(예: 새 세션 시작) 호출.</summary>
    public void ResetLastZone() => _hasShown = false;

    private void HideImmediate()
    {
        if (popupObject != null) popupObject.SetActive(false);
    }

    /// <summary>왼쪽↔오른쪽 구역만 서로 바꾼다(앞니는 그대로).</summary>
    private static UiZone MirrorLeftRight(UiZone zone)
    {
        switch (zone)
        {
            case UiZone.UpperLeft:  return UiZone.UpperRight;
            case UiZone.UpperRight: return UiZone.UpperLeft;
            case UiZone.LowerLeft:  return UiZone.LowerRight;
            case UiZone.LowerRight: return UiZone.LowerLeft;
            default:                return zone;   // Front
        }
    }
}
