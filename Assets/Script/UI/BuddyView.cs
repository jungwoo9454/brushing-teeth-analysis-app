using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버디(스티커 도감) 화면 바인딩. Buddy.prefab 루트(Canvas)에 붙인다.
///
/// 표시:
///   1) 캐릭터 — Character/Chracter Monitor/Chracter의 <see cref="CharacterStage"/>가 양치/메인 씬과 동일하게 렌더(자체 동작).
///   2) 말풍선(Talk Balloon) — 모은 스티커 수 + 최근 획득 스티커 멘트.
///   3) 카운트 텍스트 — "현재 보유 / 총(50)".
///   4) 스티커 격자(Scroll View/Viewport/Content의 50칸):
///        · 획득  → 칸 프레임 = Unlock 스프라이트, 자식 'Realsticker' 활성 + 아이콘 표시.
///        · 미획득 → 칸 프레임 = Hidden 스프라이트, 'Realsticker' 비활성.
///
/// 스티커 데이터는 <see cref="StickerMng"/>(카탈로그 + 활성 아이의 획득 기록)에서 읽는다.
/// 화면이 켜질 때마다(OnEnable) 다시 그려, 양치로 새로 해금된 스티커가 즉시 반영된다.
/// </summary>
[DisallowMultipleComponent]
public class BuddyView : MonoBehaviour
{
    [Header("스티커 칸 프레임 스프라이트 (Texture/Buddy)")]
    [Tooltip("획득한 칸의 배경 — Unlock.png")]
    [SerializeField] private Sprite unlockedFrame;
    [Tooltip("미획득(잠긴) 칸의 배경 — Hidden.png")]
    [SerializeField] private Sprite hiddenFrame;

    [Header("참조 (비우면 경로로 자동 탐색)")]
    [SerializeField] private TMP_Text countText;       // RealSpace/Text (TMP)
    [SerializeField] private TMP_Text balloonText;     // RealSpace/Character/Character Talkballoon/Text (TMP)
    [SerializeField] private Transform stickerContent; // RealSpace/Scroll View/Viewport/Content
    [SerializeField] private Button backButton;        // RealSpace/Top/Back

    private const string PathCount   = "RealSpace/Text (TMP)";
    private const string PathBalloon = "RealSpace/Character/Character Talkballoon/Text (TMP)";
    private const string PathContent = "RealSpace/Scroll View/Viewport/Content";
    private const string PathBack    = "RealSpace/Top/Back";
    private const string RealStickerName = "Realsticker";

    private void Awake()
    {
        ResolveRefs();

        // 'Back' → 홈(메인 메뉴)으로. (Setting/Report와 동일하게 상단에서 빠져나갈 수 있게)
        var back = OnboardingUtil.EnsureButton(backButton != null ? backButton.transform : transform.Find(PathBack));
        if (back != null) back.onClick.AddListener(() => AppRouter.ins.ShowScreen(AppRouter.Screen.MainMenu));
    }

    private void OnEnable() => Refresh();

    private void ResolveRefs()
    {
        if (countText == null)      countText      = FindComp<TMP_Text>(PathCount);
        if (balloonText == null)    balloonText    = FindComp<TMP_Text>(PathBalloon);
        if (stickerContent == null) stickerContent = transform.Find(PathContent);
        if (backButton == null)     backButton     = FindComp<Button>(PathBack);
    }

    /// <summary>현재 활성 아이의 획득 상태로 도감 전체를 다시 그린다.</summary>
    public void Refresh()
    {
        if (stickerContent == null) return;

        var child = UserMng.ins != null ? UserMng.ins.ActiveChild : null;
        var all = StickerMng.ins.AllStickers();   // 카탈로그 전체(없으면 빈 목록)

        int slots = stickerContent.childCount;     // 프리팹의 스티커 칸 수(=50)
        int earned = 0;

        for (int i = 0; i < slots; i++)
        {
            var slot = stickerContent.GetChild(i);
            StickerDef def = i < all.Count ? all[i] : null;
            bool isEarned = def != null && child != null && StickerMng.ins.IsEarned(child, def.id);
            if (isEarned) earned++;
            BindSlot(slot, def, isEarned);
        }

        if (countText != null) countText.text = $"{earned} / {slots}";
        if (balloonText != null) balloonText.text = BuildBalloon(child, earned);
    }

    /// <summary>한 칸을 칠한다 — 프레임(획득/잠김) + 'Realsticker'(아이콘) 활성·스프라이트.</summary>
    private void BindSlot(Transform slot, StickerDef def, bool isEarned)
    {
        var frame = slot.GetComponent<Image>();
        if (frame != null)
        {
            var sprite = isEarned ? unlockedFrame : hiddenFrame;
            if (sprite != null) frame.sprite = sprite;
        }

        var real = slot.Find(RealStickerName);
        if (real == null) return;
        real.gameObject.SetActive(isEarned);
        if (isEarned && def != null && def.sprite != null)
        {
            var img = real.GetComponent<Image>();
            if (img != null) img.sprite = def.sprite;
        }
    }

    /// <summary>말풍선 멘트 — 모은 개수 + 최근 획득 스티커.</summary>
    private string BuildBalloon(ChildProfile child, int earned)
    {
        if (earned <= 0) return "양치를 끝내면 스티커를 모을 수 있어요!";

        string line1 = $"스티커를 {earned}개나 모았어요!";
        var latest = LatestEarned(child);
        return latest != null
            ? $"{line1}\n최근에는 '{latest.displayName}'를 얻었어요!"
            : line1;
    }

    /// <summary>가장 최근에 획득한 스티커(획득 기록의 마지막 id)에 해당하는 정의. 없으면 null.</summary>
    private StickerDef LatestEarned(ChildProfile child)
    {
        if (child == null || child.earnedStickerIds == null || child.earnedStickerIds.Count == 0) return null;
        string lastId = child.earnedStickerIds[child.earnedStickerIds.Count - 1];
        foreach (var s in StickerMng.ins.AllStickers())
            if (s != null && s.id == lastId) return s;
        return null;
    }

    private T FindComp<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }
}
