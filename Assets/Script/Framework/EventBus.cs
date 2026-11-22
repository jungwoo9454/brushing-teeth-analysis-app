using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타입 기반 전역 이벤트 버스.
/// publisher와 subscriber가 서로를 직접 참조하지 않고 통신할 수 있게 한다.
/// 예) ML 파이프라인의 ZoneClassifier가 ZoneDetectedEvent를 발행하면
///     UI 쪽 코드가 같은 이벤트를 구독하여 AR 피드백을 표시한다.
/// </summary>
/// <remarks>
/// - 정적 클래스이므로 씬 전환과 무관하게 핸들러 등록 상태가 유지된다.
/// - 핸들러는 반드시 OnDisable/OnDestroy에서 Unsubscribe 해야 메모리 누수가 발생하지 않는다.
/// - 스레드 안전성은 보장하지 않으므로 메인 스레드(Unity 메시지 콜백 안)에서만 호출한다.
/// </remarks>
public static class EventBus
{
    // 이벤트 타입(T)별 핸들러 묶음.
    // Delegate 베이스 타입으로 보관하고, Publish 시 Action<T>로 캐스팅하여 호출한다.
    private static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

    /// <summary>
    /// T 타입 이벤트를 구독한다.
    /// </summary>
    /// <typeparam name="T">이벤트 페이로드 타입 (struct 권장 — GC 최소화).</typeparam>
    /// <param name="handler">이벤트 발행 시 호출될 콜백.</param>
    public static void Subscribe<T>(Action<T> handler)
    {
        // null 핸들러는 의미가 없으므로 무시하고 경고만 남긴다.
        if (handler == null)
        {
            Debug.LogWarning("[EventBus] null handler subscribe 시도 — 무시함.");
            return;
        }

        var type = typeof(T);

        // 기존 등록된 델리게이트가 있으면 결합(Combine)하고, 없으면 새로 등록한다.
        // Delegate.Combine은 새 멀티캐스트 인스턴스를 반환하므로 원본은 변경되지 않는다.
        if (handlers.TryGetValue(type, out var existing))
            handlers[type] = Delegate.Combine(existing, handler);
        else
            handlers[type] = handler;
    }

    /// <summary>
    /// T 타입 이벤트 구독을 해제한다. 등록되지 않은 핸들러를 넘기면 조용히 무시된다.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;

        var type = typeof(T);
        if (!handlers.TryGetValue(type, out var existing)) return;

        // Remove 결과로 남은 델리게이트가 없으면(=마지막 구독자 해제) 딕셔너리에서도 제거하여
        // 빈 항목이 누적되지 않게 한다.
        var removed = Delegate.Remove(existing, handler);
        if (removed == null) handlers.Remove(type);
        else handlers[type] = removed;
    }

    /// <summary>
    /// T 타입 이벤트를 모든 구독자에게 발행한다. 구독자가 없으면 아무 일도 하지 않는다.
    /// </summary>
    /// <param name="evt">이벤트 페이로드. 호출 후에는 호출자가 더 이상 신경 쓰지 않아도 된다.</param>
    public static void Publish<T>(T evt)
    {
        // 해당 타입에 대한 구독자가 없으면 즉시 반환 — 불필요한 캐스팅·호출 비용 절감.
        if (!handlers.TryGetValue(typeof(T), out var existing)) return;

        // Action<T>로 캐스팅하여 호출. Combine 결과가 멀티캐스트면 등록 순서대로 모두 호출된다.
        ((Action<T>)existing).Invoke(evt);
    }

    /// <summary>
    /// 모든 구독을 제거한다. 테스트 리셋이나 앱 강제 종료 시 정리 용도로만 사용.
    /// 일반 런타임에서는 호출하지 말 것 — 다른 시스템의 구독이 사라져 버그가 된다.
    /// </summary>
    public static void Clear() => handlers.Clear();
}
