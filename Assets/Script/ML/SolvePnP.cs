using UnityEngine;

/// <summary>
/// 3D-2D 점 대응으로 카메라 자세를 추정하는 PnP(Perspective-n-Point) 풀이.
/// OpenCV cv2.solvePnP(SOLVEPNP_ITERATIVE)를 외부 패키지 없이 순수 C#으로 대체한다 —
/// 파이썬 데모 face_mesh.py의 _estimate_head_pose가 쓰는 동작을 재현하기 위한 것.
///
/// Levenberg-Marquardt로 재투영 오차(reprojection error)를 최소화한다.
/// 좌표 규약은 OpenCV와 동일: 이미지 원점 좌상단(y 아래로 +), 카메라 z는 정면(+).
/// 출력 회전행렬도 같은 규약의 object→camera 회전(행 우선 3x3)이다.
/// </summary>
public static class SolvePnP
{
    // LM 반복 상한과 한 반복 안에서 damping(λ)을 키우며 재시도하는 횟수.
    private const int MAX_ITERATIONS = 60;
    private const int MAX_DAMPING_RETRIES = 5;

    /// <summary>
    /// objectPoints(모델 3D)와 imagePoints(픽셀 2D) 대응으로 회전행렬을 추정한다.
    /// </summary>
    /// <param name="objectPoints">모델 3D 점들. imagePoints와 같은 길이, 최소 4점.</param>
    /// <param name="imagePoints">대응하는 이미지 픽셀 점들.</param>
    /// <param name="focal">초점거리(픽셀). face_mesh.py처럼 이미지 너비를 쓰면 된다.</param>
    /// <param name="cx">주점 x (보통 이미지 너비 / 2).</param>
    /// <param name="cy">주점 y (보통 이미지 높이 / 2).</param>
    /// <param name="rotation">출력: object→camera 3x3 회전행렬(행 우선 9개 float).</param>
    /// <param name="reprojError">출력: 점당 RMS 재투영 오차(픽셀). 클수록 발산/오수렴 의심.</param>
    /// <returns>추정 성공 여부. false면 rotation은 신뢰하지 말 것.</returns>
    public static bool TryEstimate(
        Vector3[] objectPoints, Vector2[] imagePoints,
        float focal, float cx, float cy, float[] rotation, out float reprojError)
    {
        reprojError = float.MaxValue;

        int n = objectPoints == null ? 0 : objectPoints.Length;
        if (n < 4 || imagePoints == null || imagePoints.Length != n
            || rotation == null || rotation.Length < 9 || focal <= 0f)
            return false;

        // ── 초기 자세 추정 ──
        // 회전 초기값은 x축 180° 회전. 얼굴 모델(POSE_MODEL_POINTS_3D)은 y가 위로 +,
        // z가 앞으로 + 인데 OpenCV 카메라 규약은 y 아래·z 정면이라, 정면을 바라보는
        // 얼굴의 정답 회전이 단위행렬이 아니라 x축 180°에 가깝다. 단위행렬에서 시작하면
        // LM이 그 큰 회전을 못 넘고 엉뚱한 극소점에 갇힌다(재투영 오차 수십~수백 px).
        double[] R = { 1, 0, 0, 0, -1, 0, 0, 0, -1 };  // 행 우선 3x3, R_x(180°)

        // 깊이(tz): 가장 멀리 떨어진 두 점의 모델 거리 대 이미지 거리 비로 추정(수치 안정).
        int ia = 0, ib = 1;
        float bestObjD = -1f;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float d = (objectPoints[i] - objectPoints[j]).magnitude;
                if (d > bestObjD) { bestObjD = d; ia = i; ib = j; }
            }
        float imgD = (imagePoints[ia] - imagePoints[ib]).magnitude;
        if (bestObjD < 1e-6f || imgD < 1e-6f) return false;
        double tz = focal * bestObjD / imgD;

        // tx, ty: 모델 무게중심이 이미지 점들의 평균에 투영된다고 보고 역산.
        Vector3 objMean = Vector3.zero;
        Vector2 imgMean = Vector2.zero;
        for (int i = 0; i < n; i++) { objMean += objectPoints[i]; imgMean += imagePoints[i]; }
        objMean /= n; imgMean /= n;

        // 초기 R로 회전된 모델 무게중심이 이미지 평균점 방향·깊이 tz에 오도록 t를 잡는다.
        double rmx = R[0] * objMean.x + R[1] * objMean.y + R[2] * objMean.z;
        double rmy = R[3] * objMean.x + R[4] * objMean.y + R[5] * objMean.z;
        double rmz = R[6] * objMean.x + R[7] * objMean.y + R[8] * objMean.z;
        double[] t =
        {
            (imgMean.x - cx) * tz / focal - rmx,
            (imgMean.y - cy) * tz / focal - rmy,
            tz - rmz,
        };

        // ── Levenberg-Marquardt 반복 ──
        double lambda = 1e-3;
        double prevCost = ComputeCost(objectPoints, imagePoints, R, t, focal, cx, cy, n);

        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            // 정규방정식 누적: JtJ(6x6), Jtr(6). 파라미터는 (회전증분 δr 3, 평행이동증분 δt 3).
            double[,] JtJ = new double[6, 6];
            double[] Jtr = new double[6];

            for (int i = 0; i < n; i++)
            {
                Vector3 P = objectPoints[i];

                // 카메라 좌표 Xc = R*P + t.
                double xc = R[0] * P.x + R[1] * P.y + R[2] * P.z + t[0];
                double yc = R[3] * P.x + R[4] * P.y + R[5] * P.z + t[1];
                double zc = R[6] * P.x + R[7] * P.y + R[8] * P.z + t[2];
                if (zc < 1e-6) zc = 1e-6;

                // 재투영과 잔차(proj - 관측).
                double ru = (focal * xc / zc + cx) - imagePoints[i].x;
                double rv = (focal * yc / zc + cy) - imagePoints[i].y;

                // ∂proj/∂Xc.
                double duX = focal / zc;
                double duZ = -focal * xc / (zc * zc);
                double dvY = focal / zc;
                double dvZ = -focal * yc / (zc * zc);

                // R*P (= Xc - t). ∂Xc/∂δr = -[R*P]× (회전을 left-multiply로 갱신하는 규약).
                double rpx = xc - t[0], rpy = yc - t[1], rpz = zc - t[2];

                // ∂proj/∂δr = (∂proj/∂Xc) · (-[R*P]×).
                // -[R*P]× = [[0, rpz, -rpy], [-rpz, 0, rpx], [rpy, -rpx, 0]].
                double ju0 = duX * 0      + duZ * rpy;
                double ju1 = duX * rpz    + duZ * (-rpx);
                double ju2 = duX * (-rpy) + duZ * 0;
                double jv0 = dvY * (-rpz) + dvZ * rpy;
                double jv1 = dvY * 0      + dvZ * (-rpx);
                double jv2 = dvY * rpx    + dvZ * 0;

                // ∂proj/∂δt = (∂proj/∂Xc) · I.
                double ju3 = duX, ju4 = 0,   ju5 = duZ;
                double jv3 = 0,   jv4 = dvY, jv5 = dvZ;

                double[] ju = { ju0, ju1, ju2, ju3, ju4, ju5 };
                double[] jv = { jv0, jv1, jv2, jv3, jv4, jv5 };

                for (int a = 0; a < 6; a++)
                {
                    Jtr[a] += ju[a] * ru + jv[a] * rv;
                    for (int b = 0; b < 6; b++)
                        JtJ[a, b] += ju[a] * ju[b] + jv[a] * jv[b];
                }
            }

            // damping을 키우며 재시도 — cost가 줄어드는 step을 찾는다.
            bool stepAccepted = false;
            bool converged = false;
            for (int retry = 0; retry < MAX_DAMPING_RETRIES; retry++)
            {
                double[,] A = (double[,])JtJ.Clone();
                for (int d = 0; d < 6; d++) A[d, d] += lambda * A[d, d] + 1e-12;

                if (!Solve6(A, Jtr, out double[] delta)) { lambda *= 10; continue; }

                // 정규방정식 해 delta = A⁻¹Jtr 이고, 실제 갱신량은 -delta.
                double[] Rn = ApplyRotationStep(R, -delta[0], -delta[1], -delta[2]);
                double[] tn = { t[0] - delta[3], t[1] - delta[4], t[2] - delta[5] };
                double cost = ComputeCost(objectPoints, imagePoints, Rn, tn, focal, cx, cy, n);

                if (cost < prevCost)
                {
                    if (prevCost - cost < 1e-9) converged = true;
                    R = Rn; t = tn;
                    prevCost = cost;
                    lambda = System.Math.Max(lambda * 0.3, 1e-9);
                    stepAccepted = true;
                    break;
                }
                lambda *= 10;
            }

            // 더 줄일 step이 없거나 수렴 — 종료.
            if (!stepAccepted || converged) break;
        }

        // 점당 RMS 재투영 오차 — 호출자가 수렴 품질(발산 여부)을 판단하는 데 쓴다.
        reprojError = (float)System.Math.Sqrt(prevCost / n);

        for (int i = 0; i < 9; i++) rotation[i] = (float)R[i];
        return true;
    }

    /// <summary>현재 (R, t)의 총 재투영 오차 제곱합.</summary>
    private static double ComputeCost(
        Vector3[] obj, Vector2[] img, double[] R, double[] t,
        float focal, float cx, float cy, int n)
    {
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 P = obj[i];
            double xc = R[0] * P.x + R[1] * P.y + R[2] * P.z + t[0];
            double yc = R[3] * P.x + R[4] * P.y + R[5] * P.z + t[1];
            double zc = R[6] * P.x + R[7] * P.y + R[8] * P.z + t[2];
            if (zc < 1e-6) zc = 1e-6;
            double du = (focal * xc / zc + cx) - img[i].x;
            double dv = (focal * yc / zc + cy) - img[i].y;
            sum += du * du + dv * dv;
        }
        return sum;
    }

    /// <summary>
    /// 회전증분 w(=δr)를 Rodrigues 공식으로 회전행렬로 만들고 기존 R에 left-multiply한다.
    /// 반환 = Rodrigues(w) · R (행 우선 3x3).
    /// </summary>
    private static double[] ApplyRotationStep(double[] R, double wx, double wy, double wz)
    {
        double theta2 = wx * wx + wy * wy + wz * wz;
        double[] dR = new double[9];

        if (theta2 < 1e-18)
        {
            // 아주 작은 회전 — 1차 근사 I + [w]×.
            dR[0] = 1;   dR[1] = -wz; dR[2] = wy;
            dR[3] = wz;  dR[4] = 1;   dR[5] = -wx;
            dR[6] = -wy; dR[7] = wx;  dR[8] = 1;
        }
        else
        {
            double theta = System.Math.Sqrt(theta2);
            double kx = wx / theta, ky = wy / theta, kz = wz / theta;
            double s = System.Math.Sin(theta), c = System.Math.Cos(theta), c1 = 1 - c;
            // R = I + sinθ[k]× + (1-cosθ)[k]×².
            dR[0] = c + c1 * kx * kx;       dR[1] = c1 * kx * ky - s * kz;  dR[2] = c1 * kx * kz + s * ky;
            dR[3] = c1 * ky * kx + s * kz;  dR[4] = c + c1 * ky * ky;       dR[5] = c1 * ky * kz - s * kx;
            dR[6] = c1 * kz * kx - s * ky;  dR[7] = c1 * kz * ky + s * kx;  dR[8] = c + c1 * kz * kz;
        }

        // Rn = dR · R.
        double[] Rn = new double[9];
        for (int r = 0; r < 3; r++)
            for (int col = 0; col < 3; col++)
                Rn[r * 3 + col] = dR[r * 3 + 0] * R[0 * 3 + col]
                                + dR[r * 3 + 1] * R[1 * 3 + col]
                                + dR[r * 3 + 2] * R[2 * 3 + col];
        return Rn;
    }

    /// <summary>6x6 선형계 A·x = b를 부분 피벗 가우스 소거로 푼다.</summary>
    private static bool Solve6(double[,] A, double[] b, out double[] x)
    {
        const int N = 6;
        x = new double[N];

        // 증강행렬 [A | b].
        double[,] M = new double[N, N + 1];
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++) M[i, j] = A[i, j];
            M[i, N] = b[i];
        }

        for (int col = 0; col < N; col++)
        {
            // 부분 피벗 — 절댓값이 가장 큰 행을 위로.
            int piv = col;
            double best = System.Math.Abs(M[col, col]);
            for (int r = col + 1; r < N; r++)
            {
                double v = System.Math.Abs(M[r, col]);
                if (v > best) { best = v; piv = r; }
            }
            if (best < 1e-15) { x = null; return false; }   // 특이행렬.

            if (piv != col)
                for (int j = 0; j <= N; j++)
                    { double tmp = M[col, j]; M[col, j] = M[piv, j]; M[piv, j] = tmp; }

            // 다른 모든 행에서 col 성분 제거.
            for (int r = 0; r < N; r++)
            {
                if (r == col) continue;
                double factor = M[r, col] / M[col, col];
                for (int j = col; j <= N; j++) M[r, j] -= factor * M[col, j];
            }
        }

        for (int i = 0; i < N; i++) x[i] = M[i, N] / M[i, i];
        return true;
    }
}
