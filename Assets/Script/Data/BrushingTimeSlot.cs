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
    // 시간대 이름 — 저장 기록(BrushingRecord.timeSlot)·UI가 같은 문자열을 쓰도록 상수로 둔다.
    public const string Morning = "아침";
    public const string Lunch   = "점심";
    public const string Evening = "저녁";

    /// <summary>아침→점심→저녁 순서의 전체 시간대.</summary>
    public static readonly string[] All = { Morning, Lunch, Evening };

    /// <param name="hour">0~23 시(로컬).</param>
    /// <returns>"아침" / "점심" / "저녁".</returns>
    public static string Korean(int hour)
    {
        if (hour >= 6 && hour < 10) return Morning;   // 오전 6시 ~ 오전 10시
        if (hour >= 10 && hour < 16) return Lunch;    // 오전 10시 ~ 오후 4시
        return Evening;                                // 오후 4시 ~ 새벽 2시 (+ 새벽 2~6시 폴백)
    }

    /// <summary>시각(DateTimeOffset)의 시(Hour)를 기준으로 시간대를 반환.</summary>
    public static string Korean(DateTimeOffset time) => Korean(time.Hour);

    /// <summary>
    /// 시간대의 시작/끝 시(hour). 저녁의 끝은 익일 새벽 2시라 26(=24+2)으로 표현한다.
    /// 표시는 <see cref="Hour12"/>로 12시간제(오전/오후)로 변환한다.
    /// </summary>
    public static (int startHour, int endHour) Range(string slot)
    {
        switch (slot)
        {
            case Lunch:   return (10, 16);
            case Evening: return (16, 26);  // 오후 4시 ~ 익일 오전 2시
            default:      return (6, 10);   // 아침
        }
    }

    /// <summary>미완료 항목용 안내 문구. 예: "오전 6시 ~ 오전 10시 양치 완료 예정".</summary>
    public static string ScheduleText(string slot)
    {
        var (s, e) = Range(slot);
        return $"{Hour12(s)} ~ {Hour12(e)} 양치 완료 예정";
    }

    /// <summary>24시간제 시(0~25 등)를 "오전/오후 N시"로. (26 → 오전 2시)</summary>
    public static string Hour12(int hour24)
    {
        int h = ((hour24 % 24) + 24) % 24;
        string ap = h < 12 ? "오전" : "오후";
        int hh = h % 12;
        if (hh == 0) hh = 12;
        return $"{ap} {hh}시";
    }
}
