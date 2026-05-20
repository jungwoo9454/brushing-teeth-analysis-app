/// <summary>
/// MLP가 직접 출력하는 8개 분류 — 학습 코드의 ZONE_LIST 순서와 동일하게 유지해야 한다.
/// MLP의 8 logit argmax 결과를 이 enum 으로 캐스팅 (int)하여 사용한다.
/// 순서 변경 절대 금지 — 모델 출력 인덱스와 1:1 대응.
/// </summary>
public enum MlZone
{
    UpperLeft  = 0,  // 위 어금니 왼쪽 교합면
    UpperRight = 1,  // 위 어금니 오른쪽 교합면
    LowerLeft  = 2,  // 아래 어금니 왼쪽 교합면
    LowerRight = 3,  // 아래 어금니 오른쪽 교합면
    SideLeft   = 4,  // 왼쪽 어금니 협면 (UI에선 UpperLeft+LowerLeft 둘 다 가산)
    SideRight  = 5,  // 오른쪽 어금니 협면 (UI에선 UpperRight+LowerRight 둘 다 가산)
    Front      = 6,  // 앞니 (위/아래 통합)
    None       = 7,  // 양치 미검출 — engagement 실패 시에도 이 값 사용
}

/// <summary>
/// UI 카운터/시각화에서 사용할 5개 구역 — Side*는 두 개로 분기되어 사라진다.
/// AR 피드백(가상 세균 사라짐)도 이 5개 단위로 처리.
/// </summary>
public enum UiZone
{
    UpperLeft  = 0,
    UpperRight = 1,
    LowerLeft  = 2,
    LowerRight = 3,
    Front      = 4,
}
