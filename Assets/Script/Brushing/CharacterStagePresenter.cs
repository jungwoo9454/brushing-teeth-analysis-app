using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D 캐릭터(Rob13)를 전용 카메라로 RenderTexture에 그려, 2D UI의 RawImage("Chracter")에 합성하는 프리젠터.
/// 프로젝트가 URP 2D Renderer라 3D 메시를 월드에 직접 합성하기 어렵기에, 캐릭터만 별도 레이어/카메라로
/// 오프스크린 렌더해서 UI에 얹는다(전역 2D 설정을 건드리지 않음).
///
/// 양치 흐름 이벤트를 구독해 표정/모션 애니메이션을 전환한다.
///   - 세션 시작        → Hello (인사)
///   - 양치 중/잘함      → 긍정(웃음/엄지척 등)
///   - 잘 못함/부족 안내   → 부정(슬픔/도리도리 등)
///   - 세션 종료(성공)    → Win, (미흡) → DontKnow
///
/// 애니메이션은 Rob13.controller에 이미 존재하는 state 이름을 Animator.CrossFade로 직접 재생한다
/// (별도 파라미터/트랜지션 셋업 불필요). 컨트롤러 state명이 다르면 인스펙터에서 교체.
///
/// UI의 RawImage가 있는 오브젝트(또는 항상 활성인 오브젝트)에 부착한다.
/// </summary>
public class CharacterStagePresenter : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 — 합성 대상 / 캐릭터 프리팹
    // ─────────────────────────────────────────────────────────────

    [Header("출력 (UI 합성 대상)")]
    [Tooltip("캐릭터를 그릴 RawImage. 비우면 이 오브젝트의 RawImage를 사용.")]
    [SerializeField] private RawImage targetImage;

    [Header("캐릭터")]
    [Tooltip("렌더할 3D 캐릭터 프리팹(Animator 포함, 예: Rob13).")]
    [SerializeField] private GameObject characterPrefab;

    [Tooltip("스테이지에서 캐릭터를 비추는 거리/높이/회전 등 배치. 보통 기본값으로 OK, 안 보이면 조정.")]
    [SerializeField] private Vector3 characterLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 characterEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private float characterScale = 1f;

    [Header("스테이지 카메라")]
    [Tooltip("캐릭터를 담을 카메라의 로컬 위치(캐릭터 기준 정면 거리). z가 음수면 정면에서 바라본다.")]
    [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 1f, -3.2f);
    [SerializeField] private float cameraPitch = 5f;
    [SerializeField] private float cameraFov = 30f;
    [Tooltip("RenderTexture 해상도(정사각 권장 — UI 슬롯이 정사각).")]
    [SerializeField] private int renderTextureSize = 512;
    [Tooltip("배경 투명(알파) 여부. 켜면 캐릭터만 보이고 UI 배경이 비친다.")]
    [SerializeField] private bool transparentBackground = true;

    [Header("조명")]
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField, Min(0f)] private float lightIntensity = 1.1f;
    [SerializeField] private Vector3 lightEulerAngles = new Vector3(40f, 160f, 0f);

    // ─────────────────────────────────────────────────────────────
    // 인스펙터 — 상황별 애니메이션 state 이름 (Rob13.controller 기준 기본값)
    // ─────────────────────────────────────────────────────────────

    [Header("애니메이션 State 이름 (Animator Controller의 state명)")]
    [Tooltip("세션 시작 인사.")]
    [SerializeField] private string startState = "Hello";
    [Tooltip("평상시/대기 (긍정 루프). 비우면 idle 전환 안 함.")]
    [SerializeField] private string idleState = "EmoTalk1";
    [Tooltip("긍정 대화(칭찬 등)일 때 재생할 모션들 — 이 중 랜덤. 정면을 보는 긍정 표정만.")]
    [SerializeField] private string[] goodStates = { "ThumbsUp", "Laught", "Win" };
    [Tooltip("부정 대화(양치 안 함/부족/교정)일 때 재생할 모션들 — 이 중 랜덤. 단, '양치를 하고 있지 않을 때만' 나온다.")]
    [SerializeField] private string[] badStates = { "No", "Cry", "Emo Angry" };
    [Tooltip("세션 성공 종료.")]
    [SerializeField] private string finishSuccessState = "Win";
    [Tooltip("세션 미흡 종료.")]
    [SerializeField] private string finishIncompleteState = "DontKnow";

    [Header("Idle 표정 변주")]
    [Tooltip("idle 중 가끔 재생할 표정 클립들. 이 중 하나를 골라 잠깐 재생 후 idle로 복귀. 비우면 변주 안 함. " +
             "몸을 돌리거나 이동하는 모션(No/LookingFor/Dance 등)은 화면에서 옆으로 돌아 보이므로 제외하고, " +
             "정면을 보는 긍정 표정만 넣는다.")]
    [SerializeField] private string[] idleVariations = { "Laught", "ThumbsUp", "Win", "Hello" };
    [Tooltip("idle 변주 간격(초)의 최소/최대 — 이 사이 랜덤 시간마다 한 번씩 표정을 바꾼다.")]
    [SerializeField, Min(0.5f)] private float idleVariationMinInterval = 5f;
    [SerializeField, Min(0.5f)] private float idleVariationMaxInterval = 10f;
    [Tooltip("변주 표정을 보여주는 시간(초). 지나면 idle로 복귀.")]
    [SerializeField, Min(0.1f)] private float idleVariationHold = 2.5f;

    [Header("동작")]
    [Tooltip("긍정/부정 표정을 다시 트리거하기까지 최소 간격(초). 잦은 전환 방지.")]
    [SerializeField, Min(0f)] private float moodCooldown = 3f;
    [Tooltip("시작 인사 모션을 보여준 뒤 idle로 복귀하기까지의 시간(초).")]
    [SerializeField, Min(0.1f)] private float startHold = 2.5f;
    [Tooltip("긍정/부정 표정을 보여준 뒤 idle로 복귀하기까지의 시간(초).")]
    [SerializeField, Min(0.1f)] private float moodHold = 3f;
    [Tooltip("CrossFade 전환 시간(초).")]
    [SerializeField, Min(0f)] private float crossFadeSeconds = 0.2f;
    [SerializeField] private bool logEvents = false;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    private Camera _stageCamera;
    private RenderTexture _rt;
    private Animator _animator;
    private GameObject _stageRoot;     // 스테이지 전체(카메라+라이트+캐릭터) 루트 — OnDestroy에서 정리.

    private float _lastMoodTime = -999f;
    private bool _lastGood;            // 마지막으로 보여준 mood가 긍정이었는지.
    private bool _hasMood;

    // idle/변주 상태 머신 — 모든 시간은 unscaledTime 기준(양치 중 timeScale=0 대비).
    private bool _inIdle;              // 현재 idle 루프 중인지(변주는 idle일 때만).
    private float _nextIdleVarTime;    // 다음 idle 변주 시작 시각.
    private bool _inIdleVariation;     // 변주 표정을 보여주는 중인지.
    private float _revertToIdleTime;   // 이 시각이 되면 idle로 복귀(start/mood/변주 hold 공용). 0이면 예약 없음.

    // 가장 최근 ZoneDetectedEvent의 engaged — 부정 표정은 '양치를 안 하고 있을 때만' 내기 위한 게이트.
    private bool _isEngaged;

    private static readonly int StageLayer = -1; // Awake에서 NameToLayer로 채움.
    private int _stageLayer;

    // ─────────────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<RawImage>();
        _stageLayer = LayerMask.NameToLayer("CharacterStage");
        if (_stageLayer < 0) _stageLayer = 0; // 레이어 미설정 시 Default로 폴백(겹쳐 보일 수 있음).

        BuildStage();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Subscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Subscribe<CharacterDialogueEvent>(OnDialogue);
        EventBus.Subscribe<ZoneDetectedEvent>(OnZoneDetected);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BrushingSessionStartedEvent>(OnSessionStarted);
        EventBus.Unsubscribe<BrushingSessionCompletedEvent>(OnSessionCompleted);
        EventBus.Unsubscribe<ZoneDetectedEvent>(OnZoneDetected);
        EventBus.Unsubscribe<CharacterDialogueEvent>(OnDialogue);
    }

    private void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (_stageRoot != null) Destroy(_stageRoot);
    }

    // ─────────────────────────────────────────────────────────────
    // 스테이지 구성 (카메라 + 라이트 + 캐릭터를 전용 레이어에 코드로 생성)
    // ─────────────────────────────────────────────────────────────

    private void BuildStage()
    {
        if (characterPrefab == null)
        {
            Debug.LogError("[CharacterStage] characterPrefab이 비어 있다 — 캐릭터를 렌더할 수 없다.");
            return;
        }

        // RenderTexture (알파 포함).
        _rt = new RenderTexture(renderTextureSize, renderTextureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = "CharacterStageRT",
            antiAliasing = 2,
        };
        _rt.Create();
        if (targetImage != null)
        {
            targetImage.texture = _rt;
            targetImage.color = Color.white; // 기본 RawImage가 회색 틴트일 수 있어 흰색으로.
        }

        // 스테이지 루트 — UI/씬 카메라와 멀리 떨어뜨려 다른 콘텐츠가 안 잡히게 한다.
        _stageRoot = new GameObject("CharacterStage(Runtime)");
        _stageRoot.transform.position = new Vector3(5000f, 5000f, 5000f);

        // 캐릭터 인스턴스.
        var character = Instantiate(characterPrefab, _stageRoot.transform);
        character.transform.localPosition = characterLocalPosition;
        character.transform.localEulerAngles = characterEulerAngles;
        character.transform.localScale = Vector3.one * characterScale;
        SetLayerRecursive(character, _stageLayer);

        _animator = character.GetComponent<Animator>();
        if (_animator == null) _animator = character.GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogWarning("[CharacterStage] 캐릭터에 Animator가 없다 — 애니메이션 전환 불가.");
        else
            // 스테이지 캐릭터는 메인 씬 카메라에 안 보여 컬링되면 애니메이션이 멈춘다 — 항상 갱신.
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 스테이지 카메라 — 이 레이어만 렌더.
        var camGo = new GameObject("StageCamera");
        camGo.transform.SetParent(_stageRoot.transform, false);
        camGo.transform.localPosition = cameraLocalPosition;
        camGo.transform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        _stageCamera = camGo.AddComponent<Camera>();
        _stageCamera.cullingMask = 1 << _stageLayer;       // 캐릭터 레이어만.
        _stageCamera.clearFlags = transparentBackground ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        _stageCamera.backgroundColor = transparentBackground ? new Color(0, 0, 0, 0) : Color.gray;
        _stageCamera.fieldOfView = cameraFov;
        _stageCamera.nearClipPlane = 0.05f;
        _stageCamera.farClipPlane = 50f;
        _stageCamera.targetTexture = _rt;
        _stageCamera.allowHDR = false;
        _stageCamera.allowMSAA = true;

        // 전용 라이트.
        var lightGo = new GameObject("StageLight");
        lightGo.transform.SetParent(_stageRoot.transform, false);
        lightGo.transform.localEulerAngles = lightEulerAngles;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = lightColor;
        light.intensity = lightIntensity;
        light.cullingMask = 1 << _stageLayer;

        // 시작 상태 — idle 진입(변주 타이머 가동).
        GoIdle();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ─────────────────────────────────────────────────────────────
    // 이벤트 → 애니메이션
    // ─────────────────────────────────────────────────────────────

    private void OnSessionStarted(BrushingSessionStartedEvent e)
    {
        _hasMood = false;
        _inIdle = false;
        Play(startState);
        // 인사 모션을 잠깐 보여준 뒤 idle로 — startHold만큼 hold 후 자동 복귀.
        ScheduleIdleAfter(startHold);
    }

    private void OnSessionCompleted(BrushingSessionCompletedEvent e)
    {
        _inIdle = false;
        _inIdleVariation = false;
        _revertToIdleTime = 0f;
        Play(e.achievedTarget ? finishSuccessState : finishIncompleteState);
    }

    /// <summary>가장 최근 양치 여부(engaged) 추적 — 부정 표정 게이트에 사용.</summary>
    private void OnZoneDetected(ZoneDetectedEvent e) => _isEngaged = e.isEngaged;

    /// <summary>
    /// 대사 성격(말풍선)에 맞춰 캐릭터 모션을 일치시킨다.
    ///   Praise(칭찬)         → 긍정 모션
    ///   Encourage/Correct/Idle(부족·교정·"양치 안 함") → 부정 모션
    ///       단, 부정 모션은 '양치를 하고 있지 않을 때(!engaged)만' 낸다.
    ///       양치 중이면 부정 표정을 내지 않는다(아이가 닦고 있는데 혼내지 않도록).
    ///   Guide(단순 안내)      → mood 변화 없음(불필요한 전환 방지)
    /// </summary>
    private void OnDialogue(CharacterDialogueEvent e)
    {
        switch (e.kind)
        {
            case DialogueKind.Praise:
                SetMood(true);
                break;
            case DialogueKind.Encourage:
            case DialogueKind.Correct:
            case DialogueKind.Idle:
                // 부정 표정은 양치를 안 하고 있을 때만.
                if (!_isEngaged) SetMood(false);
                break;
            // Guide 등은 무시.
        }
    }

    /// <summary>긍정/부정 표정 전환 — 쿨다운 + 같은 mood 연속이면 스킵. 풀에서 랜덤 재생 후 idle 복귀.</summary>
    private void SetMood(bool good)
    {
        if (_hasMood && _lastGood == good) return;
        if (Time.unscaledTime - _lastMoodTime < moodCooldown) return;

        _lastMoodTime = Time.unscaledTime;
        _lastGood = good;
        _hasMood = true;

        _inIdle = false;
        _inIdleVariation = false;
        Play(PickRandom(good ? goodStates : badStates));
        ScheduleIdleAfter(moodHold);
    }

    /// <summary>풀에서 빈 항목이 아닌 state 하나를 랜덤 선택. 풀이 비면 null.</summary>
    private static string PickRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }

    // ─────────────────────────────────────────────────────────────
    // idle 루프 + 표정 변주
    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        float now = Time.unscaledTime;

        // start/mood/변주 표정을 보여준 뒤 idle로 복귀하는 예약 처리.
        if (_revertToIdleTime > 0f && now >= _revertToIdleTime)
        {
            _revertToIdleTime = 0f;
            GoIdle();
            return;
        }

        if (!_inIdle) return;

        // idle 변주 중이면, hold가 끝났을 때 다시 idle로(위 _revertToIdleTime 경로로 처리됨).
        if (_inIdleVariation) return;

        // idle 평상시 — 다음 변주 시각이 되면 표정 클립을 하나 골라 잠깐 재생.
        if (idleVariations != null && idleVariations.Length > 0 && now >= _nextIdleVarTime)
        {
            string clip = idleVariations[Random.Range(0, idleVariations.Length)];
            if (!string.IsNullOrEmpty(clip))
            {
                _inIdleVariation = true;
                Play(clip);
                _revertToIdleTime = now + idleVariationHold;  // hold 후 idle 복귀.
            }
            else
            {
                ScheduleNextIdleVariation();
            }
        }
    }

    /// <summary>idle 루프로 진입하고 다음 변주를 예약.</summary>
    private void GoIdle()
    {
        _inIdle = true;
        _inIdleVariation = false;
        _revertToIdleTime = 0f;
        Play(string.IsNullOrEmpty(idleState) ? startState : idleState);
        ScheduleNextIdleVariation();
    }

    /// <summary>지정 시간(초) 뒤 idle로 복귀하도록 예약(인사/긍정·부정 표정 hold용).</summary>
    private void ScheduleIdleAfter(float seconds)
    {
        _revertToIdleTime = Time.unscaledTime + Mathf.Max(0.1f, seconds);
    }

    /// <summary>다음 idle 변주 시각을 min~max 사이 랜덤으로 잡는다.</summary>
    private void ScheduleNextIdleVariation()
    {
        float min = Mathf.Min(idleVariationMinInterval, idleVariationMaxInterval);
        float max = Mathf.Max(idleVariationMinInterval, idleVariationMaxInterval);
        _nextIdleVarTime = Time.unscaledTime + Random.Range(min, max);
    }

    /// <summary>Animator state를 CrossFade로 재생. state명이 없으면 무시.</summary>
    private void Play(string stateName)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName)) return;
        if (logEvents) Debug.Log($"[CharacterStage] play: {stateName}");
        _animator.CrossFadeInFixedTime(stateName, crossFadeSeconds);
    }
}
