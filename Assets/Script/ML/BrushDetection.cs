using UnityEngine;

/// <summary>
/// YOLOv8n-pose v3 추론 결과(가장 conf가 높은 detection 1개).
/// ToothbrushDetector가 채워서 FeatureBuilder/EngagementChecker로 전달한다.
/// keypoint는 swap 적용 후 값을 담는다 — head = kp[1], handle = kp[0].
/// </summary>
public struct BrushDetection
{
    /// <summary>검출 성공 여부. false면 feature[13]=0 폴백.</summary>
    public bool hasDetection;

    /// <summary>
    /// 칫솔 bbox (픽셀). x/y/width/height. UnityEngine.Rect는 (xMin, yMin, width, height).
    /// 학습 데이터의 (x1, y1, x2, y2) 표현은 Rect.xMin/yMin/xMax/yMax로 접근.
    /// </summary>
    public Rect bbox;

    /// <summary>
    /// 칫솔모 위치 (head). 학습 라벨링 swap 보정 후 — YOLO 출력 keypoints[1].
    /// swap 안 적용하면 정확도 41% → 11%로 추락하므로 반드시 swap 적용 후 채울 것.
    /// </summary>
    public Vector2 head;

    /// <summary>칫솔 손잡이 끝 위치 (handle). swap 보정 후 — YOLO 출력 keypoints[0].</summary>
    public Vector2 handle;

    /// <summary>head keypoint 유효 여부 (YOLO의 keypoint conf 또는 (0,0) 체크 기반).</summary>
    public bool hasHead;

    /// <summary>handle keypoint 유효 여부.</summary>
    public bool hasHandle;

    /// <summary>detection confidence. YOLO conf >= MlConstants.YOLO_CONF_THRESHOLD인 것만 채움.</summary>
    public float conf;
}
