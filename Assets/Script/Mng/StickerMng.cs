using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버디 보상 매니저 — 양치를 완료할 때마다 '활성 아이'에게 랜덤 스티커 1개를 해금한다.
/// 해금 기록은 아이별(ChildProfile.earnedStickerIds)로 저장되어 아이마다 다르다.
///
/// 카탈로그(StickerCatalog)는 Resources에서 자동 로드하거나 SetCatalog로 주입한다.
/// 비어 있으면(아직 스티커 목록 미주입) 해금은 조용히 건너뛴다 — 목록만 채우면 그때부터 동작한다.
///
/// UI는 추후 작업: AllStickers / GetEarned / GetLocked / IsEarned로 획득·미획득을 조회한다.
/// Singleton&lt;T&gt; 기반이라 .ins로 접근하며 DontDestroyOnLoad로 씬을 넘어 살아남는다.
/// </summary>
public class StickerMng : Singleton<StickerMng>
{
    protected StickerMng() { }

    // 앱 시작 시(첫 씬 로드 후) 자동 생성 → 이벤트 구독. 아무도 .ins를 안 건드려도 보상이 동작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { _ = ins; }

    // Assets/Resources/StickerCatalog.asset (Resources 기준 경로)
    private const string CatalogResourcePath = "StickerCatalog";

    private StickerCatalog catalog;

    protected override void Awake()
    {
        base.Awake();   // Singleton의 DontDestroyOnLoad 등록.
        if (catalog == null) catalog = Resources.Load<StickerCatalog>(CatalogResourcePath);
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnBrushingCompleted);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnBrushingCompleted);
    }

    /// <summary>카탈로그를 코드로 주입(런타임 목록 교체/테스트용).</summary>
    public void SetCatalog(StickerCatalog c) => catalog = c;

    private void OnBrushingCompleted(BrushingSessionCompletedEvent e) => UnlockRandomForActiveChild();

    /// <summary>
    /// 활성 아이에게 아직 없는 스티커 중 하나를 랜덤으로 해금하고 그 스티커를 반환한다.
    /// 아이가 없거나/카탈로그가 비었거나/전부 획득했으면 null.
    /// </summary>
    public StickerDef UnlockRandomForActiveChild()
    {
        var child = UserMng.ins.ActiveChild;
        if (child == null) return null;

        var locked = GetLocked(child);
        if (locked.Count == 0) return null;

        var pick = locked[Random.Range(0, locked.Count)];
        child.earnedStickerIds.Add(pick.id);
        UserMng.ins.Save();

        EventBus.Publish(new StickerUnlockedEvent { childId = child.id, stickerId = pick.id, sticker = pick });
        return pick;
    }

    // ─────────────────────────────────────────────────────────────
    // 조회 API (추후 UI — 획득/미획득 스티커 표시)
    // ─────────────────────────────────────────────────────────────

    /// <summary>카탈로그의 전체 스티커(없으면 빈 배열).</summary>
    public IReadOnlyList<StickerDef> AllStickers()
        => (catalog != null && catalog.stickers != null) ? catalog.stickers : System.Array.Empty<StickerDef>();

    /// <summary>아이가 획득한 스티커 목록.</summary>
    public List<StickerDef> GetEarned(ChildProfile child) => Filter(child, earned: true);

    /// <summary>아이가 아직 못 얻은(잠긴) 스티커 목록.</summary>
    public List<StickerDef> GetLocked(ChildProfile child) => Filter(child, earned: false);

    /// <summary>특정 아이가 해당 스티커를 획득했는지.</summary>
    public bool IsEarned(ChildProfile child, string stickerId)
        => child != null && child.earnedStickerIds.Contains(stickerId);

    private List<StickerDef> Filter(ChildProfile child, bool earned)
    {
        var result = new List<StickerDef>();
        if (child == null || catalog == null || catalog.stickers == null) return result;
        foreach (var s in catalog.stickers)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            if (child.earnedStickerIds.Contains(s.id) == earned) result.Add(s);
        }
        return result;
    }
}
