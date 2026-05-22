using UnityEngine;

/// <summary>
/// ML 파이프라인 전반에서 공유되는 상수 모음.
/// AI 파트가 재검증해 알려준 값들 — 변경 시 학습 분포와 mismatch되어 정확도가 크게 떨어질 수 있다.
/// HANDOFF_TO_APP.md와 다른 값이 있는 이유는 HANDOFF 일부에 오기가 있기 때문 (예: conf 0.4).
/// </summary>
public static class MlConstants
{
    // ─────────────────────────────────────────────────────────────
    // YOLO 추론 파라미터
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// YOLOv8n-pose v3 confidence 임계. 학습/논문 환경(41.32% / 58.89%)과 일치하는 0.01 사용.
    /// HANDOFF에 적힌 0.4는 v2(rule-based) 시절 값 오기 — 사용 금지(정확도 30%대로 추락).
    /// </summary>
    public const float YOLO_CONF_THRESHOLD = 0.01f;

    /// <summary>YOLO 입력 해상도(픽셀, 정사각). 학습 시 imgsz=640.</summary>
    public const int YOLO_INPUT_SIZE = 640;

    // ─────────────────────────────────────────────────────────────
    // Engagement / Stability
    // ─────────────────────────────────────────────────────────────

    /// <summary>(칫솔∩입) / 입 면적 임계. 이 이상이면 engagement 인정.</summary>
    public const float MOUTH_OVERLAP_THRESHOLD = 0.10f;

    /// <summary>최근 N프레임의 zone 누적 윈도우 크기.</summary>
    public const int STABILITY_WINDOW = 3;

    /// <summary>윈도우 안에서 같은 zone이 이 횟수 이상 나타나야 stable로 확정.</summary>
    public const int STABILITY_MIN_COUNT = 2;

    // ─────────────────────────────────────────────────────────────
    // 추가 가드 — 학습 분포 밖 입력은 None 처리
    // ─────────────────────────────────────────────────────────────

    /// <summary>mouth_height가 이 값 미만이면 정규화 분모로 사용 불가 — feature 0으로 폴백.</summary>
    public const float MIN_MOUTH_HEIGHT = 1e-6f;

    /// <summary>옆모습 가드 임계(degree). |yaw|가 이 값 초과면 zone = None.</summary>
    public const float YAW_GUARD_DEG = 45f;

    /// <summary>입 비율(mw/mh) 하한. 이 미만이면 mouth landmark가 깨진 상태로 간주.</summary>
    public const float MOUTH_ASPECT_MIN = 0.1f;

    /// <summary>입 비율 상한. 이 초과면 landmark 깨짐.</summary>
    public const float MOUTH_ASPECT_MAX = 5.0f;

    // ─────────────────────────────────────────────────────────────
    // MediaPipe Face Landmarker landmark 인덱스 (refine_landmarks=True, 478점 기준)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 외곽 입술(OUTER_LIP) 20점 인덱스 — inference/face_mesh.py:14-17 그대로.
    /// **주의: 이 순서는 polygon 순서가 아니다.** polygon 구성 시 중심 기준 각도 정렬 필수.
    /// </summary>
    public static readonly int[] OUTER_LIP_INDICES = new int[]
    {
        61, 146, 91, 181, 84, 17, 314, 405, 321, 375,
        291, 409, 270, 269, 267, 0, 37, 39, 40, 185,
    };

    /// <summary>
    /// mouth_center 산출용 4점 인덱스 — face_mesh.py:24 그대로.
    /// 13: 상순 중앙, 14: 하순 중앙, 78: 좌측 입꼬리, 308: 우측 입꼬리.
    /// 이 4점의 정규화 좌표 평균에 (W, H) 곱하여 픽셀로 변환.
    /// </summary>
    public static readonly int[] MOUTH_CENTER_INDICES = new int[] { 13, 14, 78, 308 };

    // ─────────────────────────────────────────────────────────────
    // solvePnP 6-landmark (head pose)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// PnP에 사용할 6개 landmark 인덱스 — face_mesh.py:31-39.
    /// 1: 코끝, 152: 턱, 33: 왼눈외각, 263: 오른눈외각, 61: 왼입꼬리, 291: 오른입꼬리.
    /// </summary>
    public static readonly int[] POSE_LANDMARK_IDS = new int[] { 1, 152, 33, 263, 61, 291 };

    /// <summary>
    /// 위 6점의 canonical face 3D 좌표(mm) — face_mesh.py POSE_MODEL_POINTS_3D.
    /// solvePnP의 objectPoints로 그대로 사용. 단위는 mm이지만 PnP는 스케일 불변하여 무관.
    /// </summary>
    public static readonly Vector3[] POSE_MODEL_POINTS_3D = new Vector3[]
    {
        new Vector3(  0.0f,   0.0f,   0.0f),  // 1   코끝
        new Vector3(  0.0f, -63.6f, -12.5f),  // 152 턱
        new Vector3(-43.3f,  32.7f, -26.0f),  // 33  왼눈외각
        new Vector3( 43.3f,  32.7f, -26.0f),  // 263 오른눈외각
        new Vector3(-28.9f, -28.9f, -24.1f),  // 61  왼입꼬리
        new Vector3( 28.9f, -28.9f, -24.1f),  // 291 오른입꼬리
    };
}
