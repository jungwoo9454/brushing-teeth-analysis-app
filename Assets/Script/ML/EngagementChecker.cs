using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "칫솔이 실제로 입에 닿았는가" 판정.
/// 조건 2가지: ① head 키포인트가 mouth_polygon 내부 ② (입∩bbox)/입 면적 ≥ 0.10.
/// 둘 다 만족할 때만 MLP zone을 채택하고, 아니면 zone = None으로 강제.
/// 참조: zone_classifier_reference.py engagement 부분 + HANDOFF §8.
/// </summary>
public static class EngagementChecker
{
    // Sutherland-Hodgman clipping의 중간 버퍼 — static 재사용으로 매 프레임 GC 회피.
    // 멀티스레드 호출은 가정하지 않음 (Unity 메인 스레드 전용).
    private static readonly List<Vector2> clipBufA = new List<Vector2>(64);
    private static readonly List<Vector2> clipBufB = new List<Vector2>(64);

    /// <summary>
    /// engagement 판정.
    /// </summary>
    /// <param name="head">칫솔모(head) 픽셀 좌표.</param>
    /// <param name="bbox">칫솔 bbox (픽셀).</param>
    /// <param name="polygon">입 polygon (각도 정렬된 외곽 입술 점들, 픽셀).</param>
    /// <param name="overlapThreshold">기본 MlConstants.MOUTH_OVERLAP_THRESHOLD(0.10).</param>
    /// <param name="overlapRatio">계산된 overlap 비율(디버그용).</param>
    /// <returns>engaged 여부.</returns>
    public static bool IsEngaged(
        Vector2 head, Rect bbox, Vector2[] polygon,
        float overlapThreshold, out float overlapRatio)
    {
        overlapRatio = 0f;

        // 입력 가드: polygon이 너무 짧으면 면적/포함 판정 불가.
        if (polygon == null || polygon.Length < 3) return false;

        // 조건 ①: head가 polygon 내부인지.
        if (!PointInPolygon(head, polygon)) return false;

        // 조건 ②: overlap ratio = (입∩bbox) / 입 면적 ≥ threshold.
        float polyArea = PolygonArea(polygon);
        if (polyArea <= 0f) return false;

        float interArea = IntersectionAreaWithRect(polygon, bbox);
        overlapRatio = interArea / polyArea;

        return overlapRatio >= overlapThreshold;
    }

    /// <summary>
    /// 점이 임의 polygon 내부에 있는지 — 표준 ray casting 알고리즘.
    /// polygon은 convex일 필요 없음. 자기교차가 있으면 결과 불정확.
    /// </summary>
    public static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        // 각 변 (poly[j] → poly[i])에 대해 수평 ray와의 교차 여부 토글.
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];

            // y 범위가 p.y를 사이에 두는 변만 후보 — XOR로 한쪽만 위/아래인지 검사.
            bool yStraddle = (pi.y > p.y) != (pj.y > p.y);
            if (!yStraddle) continue;

            // 변 위에서 p.y에 해당하는 x를 선형보간으로 계산 — p.x보다 오른쪽이면 교차.
            float xCross = (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x;
            if (p.x < xCross) inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// 부호 있는 면적의 절댓값 (shoelace 공식).
    /// CW/CCW 어느 방향이든 동일한 면적 반환.
    /// </summary>
    public static float PolygonArea(Vector2[] poly)
    {
        float sum = 0f;
        int n = poly.Length;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            sum += a.x * b.y - b.x * a.y;
        }
        return Mathf.Abs(sum) * 0.5f;
    }

    /// <summary>
    /// polygon과 axis-aligned rectangle의 교집합 면적.
    /// Sutherland-Hodgman 알고리즘으로 polygon을 rect의 4개 변에 순서대로 clipping.
    /// </summary>
    public static float IntersectionAreaWithRect(Vector2[] polygon, Rect rect)
    {
        // 1) 시작 버퍼에 polygon 복사.
        clipBufA.Clear();
        for (int i = 0; i < polygon.Length; i++) clipBufA.Add(polygon[i]);

        // 2) 4개 반평면(left/right/bottom/top)으로 순차 clipping.
        //    각 단계마다 A → B로 결과 이동 후 swap.
        ClipAgainstHalfPlane(clipBufA, clipBufB, HalfPlane.MinX, rect.xMin);
        if (clipBufB.Count == 0) return 0f;

        ClipAgainstHalfPlane(clipBufB, clipBufA, HalfPlane.MaxX, rect.xMax);
        if (clipBufA.Count == 0) return 0f;

        ClipAgainstHalfPlane(clipBufA, clipBufB, HalfPlane.MinY, rect.yMin);
        if (clipBufB.Count == 0) return 0f;

        ClipAgainstHalfPlane(clipBufB, clipBufA, HalfPlane.MaxY, rect.yMax);
        if (clipBufA.Count == 0) return 0f;

        // 3) 최종 clipped polygon 면적 — shoelace.
        float sum = 0f;
        int n = clipBufA.Count;
        for (int i = 0; i < n; i++)
        {
            var a = clipBufA[i];
            var b = clipBufA[(i + 1) % n];
            sum += a.x * b.y - b.x * a.y;
        }
        return Mathf.Abs(sum) * 0.5f;
    }

    // clipping 변 종류 — Sutherland-Hodgman 내부 dispatch용.
    private enum HalfPlane { MinX, MaxX, MinY, MaxY }

    /// <summary>
    /// 입력 폴리곤을 하나의 반평면(예: x ≥ minX)에 대해 clip.
    /// 결과는 output 리스트에 채우고 input은 변경하지 않음.
    /// </summary>
    private static void ClipAgainstHalfPlane(
        List<Vector2> input, List<Vector2> output, HalfPlane plane, float bound)
    {
        output.Clear();
        if (input.Count == 0) return;

        Vector2 prev = input[input.Count - 1];
        bool prevInside = IsInside(prev, plane, bound);

        for (int i = 0; i < input.Count; i++)
        {
            Vector2 curr = input[i];
            bool currInside = IsInside(curr, plane, bound);

            // Sutherland-Hodgman 4-case 처리:
            // in→in: curr 추가. in→out: 교점만. out→in: 교점 + curr. out→out: 아무것도 안 함.
            if (currInside)
            {
                if (!prevInside) output.Add(Intersect(prev, curr, plane, bound));
                output.Add(curr);
            }
            else if (prevInside)
            {
                output.Add(Intersect(prev, curr, plane, bound));
            }

            prev = curr;
            prevInside = currInside;
        }
    }

    /// <summary>지정한 반평면에 대해 점이 안쪽(또는 경계)인지.</summary>
    private static bool IsInside(Vector2 p, HalfPlane plane, float bound)
    {
        switch (plane)
        {
            case HalfPlane.MinX: return p.x >= bound;
            case HalfPlane.MaxX: return p.x <= bound;
            case HalfPlane.MinY: return p.y >= bound;
            case HalfPlane.MaxY: return p.y <= bound;
            default: return true;
        }
    }

    /// <summary>두 점 사이의 선분이 경계선(x=bound 또는 y=bound)을 만나는 교점.</summary>
    private static Vector2 Intersect(Vector2 a, Vector2 b, HalfPlane plane, float bound)
    {
        // 수직(x=bound) 또는 수평(y=bound) 경계에 대한 선형 보간.
        if (plane == HalfPlane.MinX || plane == HalfPlane.MaxX)
        {
            // a.x → b.x 진행에서 x=bound가 되는 t.
            float denom = b.x - a.x;
            float t = Mathf.Approximately(denom, 0f) ? 0f : (bound - a.x) / denom;
            return new Vector2(bound, a.y + t * (b.y - a.y));
        }
        else
        {
            float denom = b.y - a.y;
            float t = Mathf.Approximately(denom, 0f) ? 0f : (bound - a.y) / denom;
            return new Vector2(a.x + t * (b.x - a.x), bound);
        }
    }
}
