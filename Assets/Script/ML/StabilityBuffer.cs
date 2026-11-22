using System.Collections.Generic;

/// <summary>
/// zone prediction의 시간 안정화 버퍼.
/// 최근 N프레임(STABILITY_WINDOW=3) 안에서 같은 zone이 minCount(2) 이상 나타나면
/// 그 zone을 stable로 확정. 그 외에는 직전 stable값을 유지(짧은 인식 끊김 = 깜빡임 흡수).
/// 단, 미검출(None)이 idleResetFrames(기본 3) 프레임 연속되면 "양치 멈춤"으로 보고
/// stable을 None으로 되돌린다 — 칫솔을 입에서 떼면 곧 None 상태가 된다.
/// 참조: zone_classifier_reference.py:_update_stable_zone.
/// </summary>
public class StabilityBuffer
{
    /// <summary>
    /// 연속 미검출(None) 프레임이 이 수에 도달하면 stable zone을 None으로 되돌리는 기본값.
    /// "칫솔을 입에서 뗐다 → 양치 멈춤"을 인식하는 임계. 작을수록 떼는 즉시 None에 가깝고,
    /// 클수록 짧은 인식 끊김(깜빡임)을 더 많이 흡수한다.
    /// </summary>
    public const int DEFAULT_IDLE_RESET_FRAMES = 3;

    // 최근 raw zone push 이력. None도 같이 푸시하지만 카운트엔 제외.
    // Queue 사이즈는 window를 넘지 않도록 매 Push에서 Trim.
    private readonly Queue<MlZone> history = new Queue<MlZone>();

    // 현재 확정된 stable zone. 초기값은 None — 시작 직후 prediction이 충분히 모이기 전 상태.
    private MlZone currentStable = MlZone.None;

    // 연속으로 들어온 미검출(None) 프레임 수. idleResetFrames에 도달하면 currentStable을
    // None으로 되돌린다. 유효 zone이 하나라도 들어오면 0으로 리셋.
    private int consecutiveNone;

    // 인스펙터에서 조정 가능하도록 생성자에서만 주입. 기본값은 MlConstants 사용.
    private readonly int window;
    private readonly int minCount;
    private readonly int idleResetFrames;

    /// <summary>기본 파라미터(window 3, minCount 2, idleReset 3) 생성자.</summary>
    public StabilityBuffer()
        : this(MlConstants.STABILITY_WINDOW, MlConstants.STABILITY_MIN_COUNT, DEFAULT_IDLE_RESET_FRAMES) { }

    /// <summary>커스텀 파라미터 생성자 — 튜닝/테스트용.</summary>
    public StabilityBuffer(int window, int minCount, int idleResetFrames = DEFAULT_IDLE_RESET_FRAMES)
    {
        this.window = window;
        this.minCount = minCount;
        this.idleResetFrames = idleResetFrames;
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
        // 0) 연속 미검출 추적. 유효 zone이 들어오면 카운터를 리셋하고,
        //    None이 idleResetFrames 연속되면 "양치 멈춤"으로 보고 stable을 None으로 되돌린다.
        if (raw == MlZone.None)
        {
            consecutiveNone++;
            if (consecutiveNone >= idleResetFrames)
            {
                currentStable = MlZone.None;
                history.Clear();   // 다시 양치 시작 시 옛 zone 잔재 없이 새로 잡도록 비운다.
                return currentStable;
            }
        }
        else
        {
            consecutiveNone = 0;
        }

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
        consecutiveNone = 0;
    }
}
