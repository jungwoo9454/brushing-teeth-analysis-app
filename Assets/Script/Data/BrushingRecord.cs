using System;

/// <summary>
/// 한 번의 양치 세션 결과 1건. JSON(현재) → DB(향후) 어느 저장소로도 그대로 직렬화되도록
/// Unity/특정 저장소에 의존하지 않는 순수 직렬화 POCO로 둔다.
/// (JsonUtility 직렬화를 위해 [Serializable] + public 필드)
/// </summary>
[Serializable]
public class BrushingRecord
{
    /// <summary>
    /// 레코드 고유 id. 현재 JSON 단계에서는 비워두고(DB 전환 시 PK로 채움), 정렬은 recordedAtUnix로 한다.
    /// (프로젝트 방침상 클라이언트에서 UUID를 발급하지 않으므로 기본값은 빈 문자열.)
    /// </summary>
    public string id;

    /// <summary>
    /// 이 기록을 만든 아이 프로필 id(ChildProfile.id). 아이별 기록 분리·필터링용.
    /// 과거(이 필드 도입 전) 기록은 비어 있으며, 통계에서 '첫 아이' 것으로 간주한다(BrushingStats.ForChild).
    /// </summary>
    public string childId;

    /// <summary>양치를 진행한 시각 — ISO 8601 (예: 2026-06-01T21:30:00.0000000+09:00).</summary>
    public string recordedAt;

    /// <summary>양치를 진행한 시각 — Unix epoch 초. 정렬/쿼리/DB 인덱싱용.</summary>
    public long recordedAtUnix;

    /// <summary>
    /// 양치 시간대 — "아침"(06~10) / "점심"(10~16) / "저녁"(16~익일 02). 저장 시각(로컬) 기준.
    /// 분류 기준은 <see cref="BrushingTimeSlot"/> 단일 출처.
    /// </summary>
    public string timeSlot;

    /// <summary>최종 점수 (0~100).</summary>
    public int score;

    /// <summary>실제로 양치한(칫솔이 입에 닿은) 누적 시간(초).</summary>
    public float totalBrushingSeconds;

    /// <summary>세션 전체 길이(초) — 3분 창(기본 180).</summary>
    public float sessionSeconds;

    /// <summary>가장 길게 양치를 쉰 연속 시간(초).</summary>
    public float longestIdleSeconds;

    /// <summary>
    /// 5개 UI 구역별 누적 시간(초). 인덱스 = (int)UiZone
    /// (0=왼쪽 윗니, 1=오른쪽 윗니, 2=왼쪽 아랫니, 3=오른쪽 아랫니, 4=앞니).
    /// </summary>
    public float[] zoneSeconds;

    // ── 점수 세부 항목 (0~1) — 리포트 화면/디버깅용 ──
    public float coverage01;    // 구역 충족도
    public float evenness01;    // 구역 균등도
    public float continuity01;  // 지속성(쉬지 않음)
}
