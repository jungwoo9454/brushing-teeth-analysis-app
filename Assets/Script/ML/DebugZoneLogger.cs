using System.Text;
using UnityEngine;

/// <summary>
/// EventBus의 ZoneDetectedEvent를 구독해 콘솔에 찍어주는 디버그 컴포넌트.
/// 양치 씬에 하나 배치해두면 stableZone 전환·engagement·uiZones 매핑을 즉시 검증 가능.
/// 인스펙터에서 'logEveryFrame'으로 빈도 토글.
/// </summary>
public class DebugZoneLogger : MonoBehaviour
{
    [Tooltip("true면 매 프레임 모든 이벤트를 찍는다. false면 stableZone이 바뀐 순간만 — 콘솔 노이즈 감소.")]
    [SerializeField] private bool logEveryFrame = false;

    // 직전 발행된 stableZone — 변경 감지용. 초기값 None으로 시작.
    private MlZone lastStable = MlZone.None;

    // OnEnable/OnDisable에서 구독·해제 — Object 파괴 시 EventBus 핸들러 누수를 방지.
    private void OnEnable()  => EventBus.Subscribe<ZoneDetectedEvent>(OnZone);
    private void OnDisable() => EventBus.Unsubscribe<ZoneDetectedEvent>(OnZone);

    /// <summary>이벤트 핸들러 — 조건에 맞을 때만 로그를 출력한다.</summary>
    private void OnZone(ZoneDetectedEvent e)
    {
        // 빈도 가드: 매 프레임 모드가 아니면 stableZone 전환 시점만 보고.
        if (!logEveryFrame && e.stableZone == lastStable) return;
        lastStable = e.stableZone;

        // StringBuilder로 한 줄 메시지를 조립 — string interpolation 대비 GC 약간 절약.
        var sb = new StringBuilder(128);
        sb.Append("[Zone] raw=").Append(e.rawZone)
          .Append(" stable=").Append(e.stableZone)
          .Append(" engaged=").Append(e.isEngaged)
          .Append(" face=").Append(e.hasFace)
          .Append(" brush=").Append(e.hasBrush)
          .Append(" ui=").Append(UiToString(e.uiZones));
        Debug.Log(sb.ToString());
    }

    /// <summary>UiZone 배열을 [A,B] 형태 문자열로 변환 — 빈/null은 [] 로.</summary>
    private static string UiToString(UiZone[] zones)
    {
        if (zones == null || zones.Length == 0) return "[]";
        var sb = new StringBuilder(32);
        sb.Append('[');
        for (int i = 0; i < zones.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(zones[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }
}
