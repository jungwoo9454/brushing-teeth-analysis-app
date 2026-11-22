using System;

/// <summary>
/// MLP의 8-class zone을 UI/AR에서 사용할 5-zone으로 매핑.
/// 핵심: SideLeft/SideRight는 좌·우 협면 brushing(=Bass technique)이라
///       해당 좌/우의 Upper+Lower 두 카운터에 동시 가산되어야 한다.
/// 매 프레임 할당을 피하기 위해 결과 배열은 static readonly로 캐시.
/// </summary>
public static class UIZoneMapper
{
    // 결과 배열은 호출자가 읽기 전용으로만 사용한다는 전제로 공유.
    // 외부에서 수정하면 다른 호출자의 결과도 오염되니 주의 — 필요 시 복사해서 사용.
    private static readonly UiZone[] EMPTY        = Array.Empty<UiZone>();
    private static readonly UiZone[] UPPER_LEFT   = new[] { UiZone.UpperLeft };
    private static readonly UiZone[] UPPER_RIGHT  = new[] { UiZone.UpperRight };
    private static readonly UiZone[] LOWER_LEFT   = new[] { UiZone.LowerLeft };
    private static readonly UiZone[] LOWER_RIGHT  = new[] { UiZone.LowerRight };
    private static readonly UiZone[] FRONT        = new[] { UiZone.Front };

    // SideLeft → 위/아래 어금니 왼쪽 둘 다 가산 (Bass technique 시 협면은 위/아래 어금니에 동시에 닿음).
    private static readonly UiZone[] SIDE_LEFT  = new[] { UiZone.UpperLeft,  UiZone.LowerLeft };
    private static readonly UiZone[] SIDE_RIGHT = new[] { UiZone.UpperRight, UiZone.LowerRight };

    /// <summary>
    /// 8-class MlZone → 1~2개의 UiZone 배열.
    /// None은 빈 배열 — 카운터 가산을 호출자가 자연스럽게 스킵하게 됨.
    /// </summary>
    public static UiZone[] Map(MlZone zone)
    {
        switch (zone)
        {
            case MlZone.UpperLeft:  return UPPER_LEFT;
            case MlZone.UpperRight: return UPPER_RIGHT;
            case MlZone.LowerLeft:  return LOWER_LEFT;
            case MlZone.LowerRight: return LOWER_RIGHT;
            case MlZone.SideLeft:   return SIDE_LEFT;
            case MlZone.SideRight:  return SIDE_RIGHT;
            case MlZone.Front:      return FRONT;
            case MlZone.None:
            default:                return EMPTY;
        }
    }
}
