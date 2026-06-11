using System.Collections.Generic;

/// <summary>
/// 활성 아이를 기준으로 양치 기록을 거르는 UI 보조. UserMng(활성 아이)와 BrushingStats.ForChild를 잇는다.
/// (BrushingStats는 저장소·UI에 의존하지 않는 순수 계층으로 두기 위해 UserMng 결합을 여기로 분리)
/// </summary>
public static class ChildRecords
{
    /// <summary>
    /// 현재 활성 아이의 기록만 반환한다.
    /// · 등록된 아이가 없으면 전체를 그대로 반환(필터 없음).
    /// · childId가 빈 과거 기록은 '첫 아이'의 것으로 간주(기존 데모/더미 데이터 보존).
    /// </summary>
    public static List<BrushingRecord> ActiveChildRecords(IList<BrushingRecord> all)
    {
        var um = UserMng.ins;
        var active = um != null ? um.ActiveChild : null;
        string activeId = active != null ? active.id : null;
        string fallback = (um != null && um.Children != null && um.Children.Count > 0)
            ? um.Children[0].id
            : null;
        return BrushingStats.ForChild(all, activeId, fallback);
    }
}
