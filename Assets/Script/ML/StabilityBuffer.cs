using System.Collections.Generic;

/// <summary>
/// zone prediction의 시간 안정화 버퍼.
/// 최근 N프레임(STABILITY_WINDOW=3) 안에서 같은 zone이 minCount(2) 이상 나타나면
/// 그 zone을 stable로 확정. 그 외에는 직전 stable값을 유지(null 플리커 방지).
/// 참조: zone_classifier_reference.py:_update_stable_zone.
/// </summary>
public class StabilityBuffer
{
    // 최근 raw zone push 이력. None도 같이 푸시하지만 카운트엔 제외.
    // Queue 사이즈는 window를 넘지 않도록 매 Push에서 Trim.
    private readonly Queue<MlZone> history = new Queue<MlZone>();

    // 현재 확정된 stable zone. 초기값은 None — 시작 직후 prediction이 충분히 모이기 전 상태.
    private MlZone currentStable = MlZone.None;

    // 인스펙터에서 조정 가능하도록 생성자에서만 주입. 기본값은 MlConstants 사용.
    private readonly int window;
    private readonly int minCount;

    /// <summary>기본 파라미터(3, 2) 생성자.</summary>
    public StabilityBuffer()
        : this(MlConstants.STABILITY_WINDOW, MlConstants.STABILITY_MIN_COUNT) { }

    /// <summary>커스텀 파라미터 생성자 — 튜닝/테스트용.</summary>
    public StabilityBuffer(int window, int minCount)
    {
        this.window = window;
        this.minCount = minCount;
    }

    /// <summary>현재 확정된 stable zone (외부 읽기 전용).</summary>
    public MlZone Current => currentStable;

    /// <summary>
    /// 새 raw zone을 추가하고 stable zone을 갱신한다.
    /// </summary>
    /// <param name="raw">이번 프레임의 raw prediction (engagement 적용 후의 None 포함).</param>
    /// <returns>갱신된 stable zone.</returns>
    public MlZone Push(MlZone raw)
    {
        // 1) 히스토리에 추가하고 window를 초과하면 가장 오래된 항목 제거.
        history.Enqueue(raw);
        while (history.Count > window) history.Dequeue();

        // 2) None을 제외한 zone들의 출현 횟수 집계.
        //    None만 가득 차도 currentStable은 직전 값 그대로 유지(플리커 방지).
        var counts = new Dictionary<MlZone, int>();
        foreach (var z in history)
        {
            if (z == MlZone.None) continue;
            counts.TryGetValue(z, out int c);
            counts[z] = c + 1;
        }

        // 3) 유효 카운트가 없으면 갱신 없이 기존 값 유지.
        if (counts.Count == 0) return currentStable;

        // 4) 최빈 zone의 카운트가 minCount 이상이면 stable로 확정.
        //    동률일 경우 Dictionary 순회 순서에 의존하지만, 동률 자체가 드물고
        //    어느 쪽을 골라도 다음 프레임에 다시 평가되므로 동작상 무해.
        MlZone bestZone = MlZone.None;
        int bestCount = 0;
        foreach (var kv in counts)
        {
            if (kv.Value > bestCount)
            {
                bestCount = kv.Value;
                bestZone = kv.Key;
            }
        }

        if (bestCount >= minCount) currentStable = bestZone;
        // else: 기존 stable 유지.

        return currentStable;
    }

    /// <summary>버퍼/상태를 초기화. 양치 세션 시작 시점 등에서 호출.</summary>
    public void Reset()
    {
        history.Clear();
        currentStable = MlZone.None;
    }
}
