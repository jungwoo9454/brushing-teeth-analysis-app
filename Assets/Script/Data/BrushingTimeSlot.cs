using System;

/// <summary>
/// 양치 시각을 어린이용 시간대(아침/점심/저녁)로 분류하는 순수 함수(상태 없음 → static).
/// 저장 기록(BrushingRecord)·UI 등 여러 곳이 같은 기준을 쓰도록 단일 출처로 둔다.
///
/// 경계(로컬 시각 기준):
///   - 아침 : 오전 6시 ~ 오전 10시   [6, 10)
///   - 점심 : 오전 10시 ~ 오후 4시   [10, 16)
///   - 저녁 : 오후 4시 ~ 새벽 2시    [16, 24) ∪ [0, 2)
/// 미지정 구간인 새벽 2시~6시 [2, 6)는 저녁으로 폴백한다(가장 가까운 야간 구간).
/// </summary>
public static class BrushingTimeSlot
{
    /// <param name="hour">0~23 시(로컬).</param>
    /// <returns>"아침" / "점심" / "저녁".</returns>
    public static string Korean(int hour)
    {
        if (hour >= 6 && hour < 10) return "아침";   // 오전 6시 ~ 오전 10시
        if (hour >= 10 && hour < 16) return "점심";  // 오전 10시 ~ 오후 4시
        return "저녁";                                // 오후 4시 ~ 새벽 2시 (+ 새벽 2~6시 폴백)
    }

    /// <summary>시각(DateTimeOffset)의 시(Hour)를 기준으로 시간대를 반환.</summary>
    public static string Korean(DateTimeOffset time) => Korean(time.Hour);
}
