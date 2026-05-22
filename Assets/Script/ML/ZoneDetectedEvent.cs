/// <summary>
/// 매 프레임 ML 파이프라인이 EventBus로 발행하는 결과.
/// UI/AR 쪽은 이 이벤트만 구독하여 카운터 가산·세균 제거 효과 등을 트리거한다.
/// struct로 정의해 GC 부담 없이 발행.
/// </summary>
public struct ZoneDetectedEvent
{
    /// <summary>
    /// MLP의 직접 출력 zone (engagement·stability 적용 전).
    /// 디버그/시각화용 — UI 카운터에는 stableZone을 사용할 것.
    /// </summary>
    public MlZone rawZone;

    /// <summary>
    /// engagement 체크(in_polygon + mouth_overlap≥0.10)와 stability buffer(3프레임, min_count 2)를
    /// 모두 통과한 최종 zone. 실제 사용자 양치 카운팅의 기준값.
    /// </summary>
    public MlZone stableZone;

    /// <summary>
    /// stableZone의 5-zone UI 매핑 결과. 길이 1 또는 2 (Side*는 2개로 분기).
    /// stableZone == None 이면 빈 배열.
    /// </summary>
    public UiZone[] uiZones;

    // 칫솔이 입에 닿았는지(in_polygon AND mouth_overlap≥0.10) — UI에서 '양치 중' 인디케이터에 사용 가능.
    public bool isEngaged;

    // 얼굴(MediaPipe) 검출 성공 여부.
    public bool hasFace;

    // 칫솔(YOLO) 검출 성공 여부.
    public bool hasBrush;
}
