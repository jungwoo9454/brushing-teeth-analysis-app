using UnityEngine;

/// <summary>
/// IFaceLandmarksProvider 임시 스텁(stub).
/// 실제 MediaPipe Runner를 연결하기 전까지 BrushingPipeline 슬롯에 끼워두기 위한 placeholder.
/// TryGetLatest는 항상 false를 반환 → 파이프라인이 face=invalid 경로로 동작하여
/// UI/이벤트/Stability/Engagement 흐름 자체를 검증할 수 있게 한다.
/// MediaPipe Runner가 준비되면 이 컴포넌트를 해당 Runner의 IFaceLandmarksProvider 구현으로 교체할 것.
/// </summary>
public class DummyFaceProvider : MonoBehaviour, IFaceLandmarksProvider
{
    /// <summary>
    /// 항상 false를 반환 — 실제 landmark가 없으므로 BrushingPipeline은 PublishEmpty 경로로 진입.
    /// </summary>
    public bool TryGetLatest(out Vector3[] normalizedLandmarks, out Matrix4x4? faceTransform)
    {
        // 호출자가 결과를 읽지 않도록 명시적으로 null 채우기.
        normalizedLandmarks = null;
        faceTransform = null;
        return false;
    }
}
