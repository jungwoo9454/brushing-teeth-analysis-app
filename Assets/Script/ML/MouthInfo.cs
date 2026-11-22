using UnityEngine;

/// <summary>
/// MediaPipe Face Landmarker 결과에서 추출한 입(mouth) 정보.
/// MediaPipeFacePort가 매 프레임 채워서 FeatureBuilder/EngagementChecker로 전달한다.
/// 좌표 단위는 **픽셀** (정규화 좌표 × W/H 적용 완료) — 학습 feature 추출 코드와 일치.
/// </summary>
public struct MouthInfo
{
    /// <summary>얼굴 검출 성공 여부. false면 다른 필드 무시.</summary>
    public bool isValid;

    /// <summary>
    /// OUTER_LIP 20점을 각도 정렬한 polygon (픽셀 좌표).
    /// 정렬 안 된 raw 인덱스 순서를 그대로 쓰면 자기교차가 일어나니 반드시 정렬 후 채울 것.
    /// </summary>
    public Vector2[] polygon;

    /// <summary>
    /// 입 중심 (픽셀). MediaPipe landmark [13, 14, 78, 308] 4점의 평균.
    /// 주의: polygon centroid나 polygon bbox center가 아님.
    /// </summary>
    public Vector2 center;

    /// <summary>polygon의 axis-aligned bbox width (max(x) - min(x)).</summary>
    public float width;

    /// <summary>polygon의 axis-aligned bbox height (max(y) - min(y)). feature 정규화의 기본 분모.</summary>
    public float height;

    /// <summary>solvePnP로 얻은 pitch (radian). 양수=머리 위로(low-angle), 음수=앞으로 숙임.</summary>
    public float pitch;

    /// <summary>yaw (radian). 옆모습 가드(abs(yaw) > 45°) 판정에 사용.</summary>
    public float yaw;
}
