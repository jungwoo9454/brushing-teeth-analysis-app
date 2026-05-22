using UnityEngine;

/// <summary>
/// MouthInfo + BrushDetection → 14차원 MLP 입력 벡터 변환기.
/// 학습 코드(scripts/extract_train_features.py의 extract_features())와 동일한 산식 사용.
/// 어느 한 단계라도 정규화 분모가 0이거나 detection이 없으면 안전한 폴백 값을 채운다.
/// </summary>
public static class FeatureBuilder
{
    /// <summary>14-D feature 벡터의 차원 수. MLP input dim 과 일치해야 함.</summary>
    public const int FEATURE_DIM = 14;

    /// <summary>
    /// 14차원 feature 벡터를 빌드한다.
    /// 호출자가 미리 할당한 배열을 재사용하도록 ref 버퍼를 받는다(매 프레임 GC 회피).
    /// </summary>
    /// <param name="face">MediaPipe에서 추출한 입 정보. isValid=false 면 전부 0.</param>
    /// <param name="det">YOLO 검출 결과. hasDetection=false 면 feat[13]=0 외 0.</param>
    /// <param name="feat">14 길이 출력 버퍼. 호출 전 길이 검증 책임은 호출자.</param>
    public static void Build(in MouthInfo face, in BrushDetection det, float[] feat)
    {
        // 전체 0으로 초기화 — 어떤 단계에서 일찍 return해도 안전한 출력 보장.
        System.Array.Clear(feat, 0, FEATURE_DIM);

        // 1) 얼굴 자체가 검출 안 되면 14개 모두 0. feature[13]=0(검출 없음)도 동일.
        if (!face.isValid) return;

        float mw = face.width;
        float mh = face.height;

        // 2) 정규화 분모가 0에 가까우면 feature 추출 불가 — 0으로 폴백.
        if (mh < MlConstants.MIN_MOUTH_HEIGHT) return;

        // mouth 자체 정보는 detection 유무와 무관하게 채울 수 있음.
        feat[8] = mw / Mathf.Max(mh, MlConstants.MIN_MOUTH_HEIGHT);  // mouth aspect
        feat[9] = face.pitch;                                         // radian 그대로

        // 3) YOLO detection이 없으면 feat[13]=0 으로 학습 시 본 "검출 실패" 케이스 재현.
        //    MLP가 이 케이스도 학습했으므로 None을 잘 출력함.
        if (!det.hasDetection || !det.hasHead)
        {
            feat[13] = 0f;
            return;
        }
        feat[13] = 1f;

        float mcx = face.center.x;
        float mcy = face.center.y;
        Vector2 head = det.head;
        Vector2 handle = det.handle;

        // bbox는 (xMin, yMin, width, height). 학습 코드는 (x1, y1, x2, y2)로 다루므로
        // bbox 중심/너비/높이를 Rect 필드에서 직접 가져온다.
        float bcx = det.bbox.xMin + det.bbox.width  * 0.5f;
        float bcy = det.bbox.yMin + det.bbox.height * 0.5f;
        float bw  = det.bbox.width;
        float bh  = det.bbox.height;

        // feat[0..1]: head 키포인트 상대 좌표 / mouth_height.
        feat[0] = (head.x - mcx) / mh;
        feat[1] = (head.y - mcy) / mh;

        // feat[2..3], feat[10..12]: handle이 있어야 의미가 있는 항목.
        if (det.hasHandle)
        {
            feat[2] = (handle.x - mcx) / mh;
            feat[3] = (handle.y - mcy) / mh;

            // brush direction unit vector — head 방향 단위벡터.
            float dx = head.x - handle.x;
            float dy = head.y - handle.y;
            float norm = Mathf.Sqrt(dx * dx + dy * dy);
            if (norm > 1e-6f)
            {
                feat[10] = dx / norm;
                feat[11] = dy / norm;
            }
            // brush length는 norm 자체를 mouth_height로 정규화.
            feat[12] = norm / mh;
        }

        // feat[4..5]: bbox 중심 상대 좌표 — 칫솔의 입 기준 위치를 거시적으로 나타냄.
        feat[4] = (bcx - mcx) / mh;
        feat[5] = (bcy - mcy) / mh;

        // feat[6]: bbox 너비는 **mouth_width** 기준 정규화 (다른 feature와 달리 mh가 아님).
        feat[6] = bw / Mathf.Max(mw, MlConstants.MIN_MOUTH_HEIGHT);

        // feat[7]: bbox 높이는 mouth_height 기준.
        feat[7] = bh / mh;
    }
}
