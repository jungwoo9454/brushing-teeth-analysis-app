using System;
using UnityEngine;

/// <summary>
/// MediaPipe Face Landmarker 결과(478 정규화 landmark + optional 4x4 transform)를
/// 학습 코드(face_mesh.py)와 동일한 의미의 MouthInfo로 변환한다.
///
/// 핵심 처리:
///   ① mouth_polygon: OUTER_LIP_INDICES 20점 → 픽셀 변환 → **각도 정렬** (자기교차 방지)
///   ② mouth_center: [13, 14, 78, 308] 4점의 정규화 좌표 평균 → 픽셀
///   ③ mouth_width/height: mouth_polygon의 axis-aligned bbox dim
///   ④ pitch/yaw: facial_transformation_matrix가 있으면 회전 부분에서 Euler 추출, 없으면 0 폴백
/// </summary>
public static class MediaPipeFacePort
{
    /// <summary>refine_landmarks=True 사용 시 478개 — 그 이하는 입력 invalid로 간주.</summary>
    private const int REQUIRED_LANDMARK_COUNT = 478;

    /// <summary>
    /// solvePnP 결과의 점당 RMS 재투영 오차가 이 픽셀값을 넘으면 발산/오수렴으로 보고
    /// pitch/yaw를 0(정면)으로 폴백한다. 정상 수렴은 보통 10px 안쪽.
    /// </summary>
    private const float MAX_REPROJ_ERROR = 50f;

    /// <summary>
    /// 한 프레임의 MediaPipe 결과를 MouthInfo로 변환.
    /// </summary>
    /// <param name="landmarks">478개 정규화 landmark. x, y ∈ [0, 1].</param>
    /// <param name="imageW">원본 이미지 너비 (픽셀).</param>
    /// <param name="imageH">원본 이미지 높이 (픽셀).</param>
    /// <param name="faceTransform">FaceLandmarker의 4x4 transformation matrix. 없으면 null.</param>
    /// <returns>isValid=false면 다른 필드 무시.</returns>
    public static MouthInfo Build(
        Vector3[] landmarks, int imageW, int imageH)
    {
        var info = new MouthInfo { isValid = false };

        // 입력 가드 — landmark 부족하거나 해상도 0이면 즉시 invalid.
        if (landmarks == null || landmarks.Length < REQUIRED_LANDMARK_COUNT) return info;
        if (imageW <= 0 || imageH <= 0) return info;

        // 1) mouth_polygon — 인덱스 추출 후 각도 정렬.
        var polygon = BuildSortedMouthPolygon(landmarks, imageW, imageH);
        if (polygon == null) return info;

        // 2) mouth_center — 4점 평균 (polygon centroid나 bbox center가 아님에 유의).
        var center = ComputeMouthCenter4Avg(landmarks, imageW, imageH);

        // 3) mouth_width / height — polygon의 axis-aligned bbox 차원.
        ComputeBboxDim(polygon, out float mw, out float mh);
        // 너무 좁은 입은 정규화 분모로 쓸 수 없으니 invalid로 처리.
        if (mh < MlConstants.MIN_MOUTH_HEIGHT) return info;

        // 4) pitch / yaw — solvePnP로 추정 (face_mesh.py _estimate_head_pose와 동일).
        EstimateHeadPose(landmarks, imageW, imageH, out float pitch, out float yaw);

        info.isValid = true;
        info.polygon = polygon;
        info.center  = center;
        info.width   = mw;
        info.height  = mh;
        info.pitch   = pitch;
        info.yaw     = yaw;
        return info;
    }

    /// <summary>
    /// OUTER_LIP_INDICES 20점을 픽셀 좌표로 변환 후 중심 기준 각도(atan2)로 오름차순 정렬.
    /// 원래 인덱스 순서는 polygon 순서가 아니므로 그대로 그리면 자기교차가 발생한다(AI 파트 답변).
    /// </summary>
    private static Vector2[] BuildSortedMouthPolygon(Vector3[] L, int W, int H)
    {
        int n = MlConstants.OUTER_LIP_INDICES.Length;
        var pts = new Vector2[n];

        // 정규화 좌표 → 픽셀 좌표.
        for (int i = 0; i < n; i++)
        {
            int idx = MlConstants.OUTER_LIP_INDICES[i];
            // landmark 배열이 짧으면 안전하게 polygon 빌드 실패 처리.
            if (idx >= L.Length) return null;
            var v = L[idx];
            pts[i] = new Vector2(v.x * W, v.y * H);
        }

        // 중심(평균) 좌표 계산 — 정렬 기준점.
        float cx = 0f, cy = 0f;
        for (int i = 0; i < n; i++) { cx += pts[i].x; cy += pts[i].y; }
        cx /= n; cy /= n;

        // 각도(key) + 인덱스(value) 쌍으로 Array.Sort<TKey, TValue> 호출 → indices가 각도 순으로 재배치.
        // 폴리곤 크기가 20점으로 작아 GC 부담은 미미하지만, 매 프레임이라 추후 캐시화 여지 있음.
        var angles = new float[n];
        var indices = new int[n];
        for (int i = 0; i < n; i++)
        {
            angles[i]  = Mathf.Atan2(pts[i].y - cy, pts[i].x - cx);
            indices[i] = i;
        }
        Array.Sort(angles, indices);

        // 정렬된 순서로 새 배열 구성하여 반환.
        var sorted = new Vector2[n];
        for (int i = 0; i < n; i++) sorted[i] = pts[indices[i]];
        return sorted;
    }

    /// <summary>
    /// MOUTH_CENTER_INDICES 4점(13: 상순 중앙, 14: 하순 중앙, 78: 왼 입꼬리, 308: 오른 입꼬리)의
    /// 정규화 좌표 평균에 (W, H)를 곱해 픽셀 좌표 반환.
    /// 학습 코드(face_mesh.py:24)와 동일한 산식.
    /// </summary>
    private static Vector2 ComputeMouthCenter4Avg(Vector3[] L, int W, int H)
    {
        int n = MlConstants.MOUTH_CENTER_INDICES.Length;
        float sx = 0f, sy = 0f;
        for (int i = 0; i < n; i++)
        {
            int idx = MlConstants.MOUTH_CENTER_INDICES[i];
            if (idx >= L.Length) return Vector2.zero;  // 안전 폴백 — 정상 입력에서는 발생 안 함.
            sx += L[idx].x;
            sy += L[idx].y;
        }
        return new Vector2(sx / n * W, sy / n * H);
    }

    /// <summary>polygon의 axis-aligned bounding box 너비/높이.</summary>
    private static void ComputeBboxDim(Vector2[] poly, out float w, out float h)
    {
        float minX = poly[0].x, maxX = poly[0].x;
        float minY = poly[0].y, maxY = poly[0].y;
        // 1번 인덱스부터 min/max 확장. else로 분기해 비교 횟수 절반.
        for (int i = 1; i < poly.Length; i++)
        {
            if      (poly[i].x < minX) minX = poly[i].x;
            else if (poly[i].x > maxX) maxX = poly[i].x;
            if      (poly[i].y < minY) minY = poly[i].y;
            else if (poly[i].y > maxY) maxY = poly[i].y;
        }
        w = maxX - minX;
        h = maxY - minY;
    }

    /// <summary>
    /// solvePnP로 head pitch/yaw(라디안)를 추정한다 — 파이썬 데모 face_mesh.py의
    /// _estimate_head_pose와 동일한 방식. POSE_LANDMARK_IDS 6점을 POSE_MODEL_POINTS_3D에
    /// 맞춰 PnP를 풀고, object→camera 회전행렬에서 오일러각을 뽑는다.
    /// focal=이미지 너비, 주점=이미지 중심 (face_mesh.py와 동일한 카메라 가정).
    ///
    /// MLP의 feature[9](pitch)는 이 solvePnP 값으로 학습됐으므로, 추론도 동일 방식으로
    /// 맞춰야 입력 분포가 일치한다. (이전엔 MediaPipe facialTransformationMatrix를 썼다.)
    /// </summary>
    private static void EstimateHeadPose(Vector3[] L, int W, int H, out float pitch, out float yaw)
    {
        pitch = 0f;
        yaw = 0f;

        // POSE_LANDMARK_IDS 6점의 픽셀 좌표를 모은다. landmark가 부족하면 0 폴백.
        var ids = MlConstants.POSE_LANDMARK_IDS;
        var imagePts = new Vector2[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] >= L.Length) return;
            imagePts[i] = new Vector2(L[ids[i]].x * W, L[ids[i]].y * H);
        }

        // face_mesh.py와 동일: focal=이미지 너비, 주점=이미지 중심.
        var R = new float[9];
        if (!SolvePnP.TryEstimate(
                MlConstants.POSE_MODEL_POINTS_3D, imagePts, W, W * 0.5f, H * 0.5f, R,
                out float reprojErr))
            return;

        // object→camera 회전행렬(행 우선) → 오일러각. face_mesh.py와 동일한 식:
        //   sy = sqrt(R00² + R10²),  pitch = atan2(-R20, sy),  yaw = atan2(R10, R00).
        float sy = Mathf.Sqrt(R[0] * R[0] + R[3] * R[3]);
        float rawPitch = Mathf.Atan2(-R[6], sy);
        float rawYaw = (sy > 1e-6f) ? Mathf.Atan2(R[3], R[0]) : 0f;

        // [임시 검증] reprojErr 분포 확인용 — 검증 후 이 로그는 제거.
        Debug.Log($"[Pitch] reprojErr={reprojErr:F1}px pitch={rawPitch * Mathf.Rad2Deg:F1}deg " +
                  $"yaw={rawYaw * Mathf.Rad2Deg:F1}deg");

        // 재투영 오차가 크면 solvePnP가 발산/오수렴한 것 — pitch/yaw를 0(정면)으로 폴백.
        if (reprojErr > MAX_REPROJ_ERROR)
            return;

        pitch = rawPitch;
        yaw = rawYaw;
    }
}
