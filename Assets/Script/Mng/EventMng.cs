using System;
using UnityEngine;

/// <summary>
/// 전역 이벤트 버스(EventBus)의 매니저 래퍼.
/// 인스펙터에서 로그 토글 같은 디버깅 옵션을 노출하기 위한 얇은 래퍼이며,
/// 실제 발행/구독은 정적 EventBus를 직접 호출해도 동일하게 동작한다.
/// </summary>
public class EventMng : Singleton<EventMng>
{
    // Singleton 외부 생성을 막기 위한 protected 생성자 (프로젝트 컨벤션).
    protected EventMng() { }

    // 모든 이벤트 발행을 콘솔에 찍을지 여부. 빌드에서는 끄는 것을 권장.
    [Tooltip("켜면 모든 이벤트 발행이 콘솔에 로그로 남는다. 빌드에서는 끄는 것을 권장.")]
    [SerializeField] private bool logEvents = false;

    /// <summary>
    /// EventBus.Publish를 감싸면서 옵션에 따라 디버그 로그를 남긴다.
    /// 호출 비용은 정적 EventBus 직접 호출보다 약간 높지만,
    /// 통합 디버깅 지점을 한 곳에 두기 위해 유지.
    /// </summary>
    public void Publish<T>(T evt)
    {
        // 옵션 켜진 경우에만 로그 — string interpolation 비용을 조건부로 회피.
        if (logEvents) Debug.Log($"[EventMng] Publish<{typeof(T).Name}>: {evt}");
        EventBus.Publish(evt);
    }

    /// <summary>EventBus.Subscribe로 패스스루.</summary>
    public void Subscribe<T>(Action<T> handler) => EventBus.Subscribe(handler);

    /// <summary>EventBus.Unsubscribe로 패스스루.</summary>
    public void Unsubscribe<T>(Action<T> handler) => EventBus.Unsubscribe(handler);
}
