using UnityEngine;

/// <summary>
/// MediaPipe Face Landmarker 결과를 BrushingPipeline에 공급하는 인터페이스.
/// 외부 패키지(homuler/MediaPipeUnity 등)의 정확한 타입에 ML 파이프라인이 의존하지 않도록 분리한 어댑터 경계.
/// 사용자가 자신의 MediaPipe Runner(FaceLandmarkerRunner 등) 위에 이 인터페이스를 구현하고
/// BrushingPipeline의 SerializeField에 드래그한다.
///
/// 학습 분포와의 일치를 위해 Runner는 다음 설정으로 동작해야 한다 (HANDOFF):
///   - refine_landmarks = True (478 landmark)
///   - max_num_faces = 1
///   - min_detection_confidence = 0.5
/// </summary>
public interface IFaceLandmarksProvider
{
    /// <summary>
    /// 가장 최근 프레임의 face landmark detection 결과를 가져온다.
    /// </summary>
    /// <param name="normalizedLandmarks">
    /// 478개 정규화 landmark. x, y ∈ [0, 1] (z는 깊이, 사용 안 함).
    /// 호출자(BrushingPipeline)는 이 배열을 읽기 전용으로만 사용하므로
    /// Runner가 내부 버퍼를 그대로 노출해도 안전 — 다음 프레임에 덮어써도 무방.
    /// </param>
    /// <param name="faceTransform">
    /// FaceLandmarker의 facial_transformation_matrix (4x4). pitch/yaw 추출에 사용.
    /// Runner가 제공하지 않으면 null로 둘 것 — 이 경우 pitch는 0으로 폴백된다.
    /// </param>
    /// <returns>이번 프레임에 유효한 detection이 있으면 true, 그 외 false.</returns>
    bool TryGetLatest(out Vector3[] normalizedLandmarks, out Matrix4x4? faceTransform);
}
