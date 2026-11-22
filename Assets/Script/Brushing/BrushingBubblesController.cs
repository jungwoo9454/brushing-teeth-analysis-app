using UnityEngine;

/// <summary>
/// 양치하는 '중'에만 버블 파티클이 올라오게 한다.
///   - onlyWhenEngaged=true(기본): 실제로 양치 중(칫솔이 입에 닿아 zone이 잡힐 때)에만 버블 emit.
///   - onlyWhenEngaged=false: 양치 세션이 진행되는 동안 계속 emit.
/// 양치를 안 할 때(세션 밖 또는 칫솔을 떼면)는 새 버블을 멈춘다(이미 올라오던 건 자연스럽게 사라짐).
///
/// BrushingBubbles 파티클 프리팹 루트(ParticleSystem이 있는 오브젝트)에 부착한다.
/// 양치 씬의 화면 하단(카메라 앞)에 배치하면 아래에서 위로 버블이 올라온다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[DisallowMultipleComponent]
public class BrushingBubblesController : MonoBehaviour
{
    [Tooltip("켜면 '실제 양치 중(engaged)'에만, 끄면 '세션 동안 내내' 버블을 낸다.")]
    [SerializeField] private bool onlyWhenEngaged = true;

    private ParticleSystem _ps;
    private bool _sessionActive;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        SetEmitting(false);   // 시작은 정지 — 양치 전엔 버블 없음.
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Subscribe<ZoneDetectedEvent>(OnZoneDetected);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZoneDetected);
    }

    private void OnSessionStarted(BrushingSessionStartedEvent e)
    {
        _sessionActive = true;
        if (!onlyWhenEngaged) SetEmitting(true);
    }

    private void OnSessionCompleted(BrushingSessionCompletedEvent e)
    {
        _sessionActive = false;
        SetEmitting(false);
    }

    private void OnZoneDetected(ZoneDetectedEvent e)
    {
        if (onlyWhenEngaged) SetEmitting(_sessionActive && e.isEngaged);
    }

    /// <summary>emission만 켜고 끈다 — 이미 떠 있는 버블은 그대로 올라가다 수명대로 사라진다.</summary>
    private void SetEmitting(bool on)
    {
        if (_ps == null) return;
        var em = _ps.emission;
        em.enabled = on;
        if (on && !_ps.isPlaying) _ps.Play();
    }
}
