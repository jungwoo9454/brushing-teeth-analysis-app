/// <summary>
/// UiZone → 한글 라벨의 단일 출처(single source of truth).
/// 상태 오버레이·대사·진행바 등 여러 UI가 같은 구역명을 쓰도록 여기서만 정의한다.
/// </summary>
public static class ZoneLabels
{
    /// <summary>UiZone enum → 어린이용 한글 구역명.</summary>
    public static string Korean(UiZone zone)
    {
        switch (zone)
        {
            case UiZone.UpperLeft:  return "왼쪽 윗니";
            case UiZone.UpperRight: return "오른쪽 윗니";
            case UiZone.LowerLeft:  return "왼쪽 아랫니";
            case UiZone.LowerRight: return "오른쪽 아랫니";
            case UiZone.Front:      return "앞니";
            default:                return zone.ToString();
        }
    }
}
