using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3D 캐릭터(Rob13 등)를 전용 레이어/카메라로 RenderTexture에 오프스크린 렌더해
/// 2D UI의 RawImage에 합성하는 공용 캐릭터 스테이지.
/// 프로젝트가 URP 2D Renderer라 3D 메시를 월드에 직접 합성하기 어렵기에, 캐릭터만 별도
/// 레이어/카메라로 렌더해서 UI에 얹는다(전역 2D 설정을 건드리지 않음).
///
/// 이 컴포넌트는 그 자체로 동작한다: 부착 즉시 스테이지를 만들고 idle 루프를 돌리며,
/// 일정 주기마다 idleVariations 중 하나를 랜덤 재생한다 — "캐릭터가 가끔 표정을 짓는"
/// 화면(메뉴/결과 화면 등)에 그대로 부착하면 된다.
///
/// 상황 반응(양치 흐름 등)이 필요하면 이 클래스를 상속해 이벤트 → 모션을 연결한다
/// (예: <see cref="CharacterStagePresenter"/>). 하위 클래스는 protected 재생 API
/// (Play / PlayTransient / PlayPersistent / GoIdle / PickRandom)만 호출하면 된다.
///
/// 애니메이션은 Animator Controller에 이미 존재하는 state 이름을 CrossFade로 직접 재생한다
/// (별도 파라미터/트랜지션 셋업 불필요). "CharacterStage" 레이어가 프로젝트에 등록돼 있어야
/// 한다(없으면 Default로 폴백 — 다른 콘텐츠와 겹쳐 보일 수 있음).
///
/// RawImage가 있는 오브젝트(또는 항상 활성인 오브젝트)에 부착한다.
/// </summary>
public class CharacterStage : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 인스펙터 — 합성 대상 / 캐릭터
    // ─────────────────────────────────────────────────────────────

    [Header("출력 (UI 합성 대상)")]
    [Tooltip("캐릭터를 그릴 RawImage. 비우면 이 오브젝트의 RawImage를 사용.")]
    [SerializeField] private RawImage targetImage;

    [Header("캐릭터")]
    [Tooltip("렌더할 3D 캐릭터 프리팹(Animator 포함, 예: Rob13).")]
    [SerializeField] private GameObject characterPrefab;

    [Tooltip("스테이지에서 캐릭터 배치(위치/회전/스케일). 안 보이면 조정.")]
    [SerializeField] private Vector3 characterLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 characterEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private float characterScale = 1f;

    [Header("스테이지 카메라")]
    [Tooltip("캐릭터를 담을 카메라의 로컬 위치(z 음수면 정면에서 바라본다).")]
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
    // 인스펙터 — idle 루프 + 주기적 표정 변주
    // ─────────────────────────────────────────────────────────────

    [Header("Idle 루프 / 표정 변주")]
    [Tooltip("평상시 재생할 idle state. 비우면 idle 전환 안 함.")]
    [SerializeField] private string idleState = "EmoTalk1";
    [Tooltip("idle 중 주기적으로 랜덤 재생할 표정 클립들. 이 중 하나를 골라 잠깐 재생 후 idle로 복귀. " +
             "몸을 돌리거나 이동하는 모션(No/LookingFor/Dance 등)은 옆으로 돌아 보이므로 제외하고 정면 긍정 표정만.")]
    [SerializeField] private string[] idleVariations = { "Laught", "ThumbsUp", "Win", "Hello" };
    [Tooltip("idle 변주 간격(초)의 최소/최대 — 이 사이 랜덤 시간마다 한 번씩 표정을 바꾼다.")]
    [SerializeField, Min(0.5f)] private float idleVariationMinInterval = 5f;
    [SerializeField, Min(0.5f)] private float idleVariationMaxInterval = 10f;
    [Tooltip("변주 표정을 보여주는 시간(초). 지나면 idle로 복귀.")]
    [SerializeField, Min(0.1f)] private float idleVariationHold = 2.5f;

    [Header("동작")]
    [Tooltip("CrossFade 전환 시간(초).")]
    [SerializeField, Min(0f)] private float crossFadeSeconds = 0.2f;
    [SerializeField] protected bool logEvents = false;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    private Camera _stageCamera;
    private RenderTexture _rt;
    private Animator _animator;
    private GameObject _stageRoot;     // 스테이지 전체(카메라+라이트+캐릭터) 루트.
    private int _stageLayer;

    // idle/변주 상태 머신 — 모든 시간은 unscaledTime 기준(양치 중 timeScale=0 대비).
    private bool _inIdle;              // 현재 idle 루프 중인지(변주는 idle일 때만).
    private float _nextIdleVarTime;    // 다음 idle 변주 시작 시각.
    private bool _inIdleVariation;     // 변주 표정을 보여주는 중인지.
    private float _revertToIdleTime;   // 이 시각에 idle로 복귀(transient/변주 hold 공용). 0이면 예약 없음.

    // 여러 스테이지가 같은 좌표/레이어에 겹쳐 서로의 카메라에 잡히지 않도록, 인스턴스마다 멀리 띄운다.
    private static int _stageCount;

    /// <summary>캐릭터에 Animator가 붙어 있는지(하위 클래스 가드용).</summary>
    protected bool HasAnimator => _animator != null;

    // ─────────────────────────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<RawImage>();
        _stageLayer = LayerMask.NameToLayer("CharacterStage");
        if (_stageLayer < 0) _stageLayer = 0; // 레이어 미설정 시 Default로 폴백.

        BuildStage();
    }

    protected virtual void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (_stageRoot != null) Destroy(_stageRoot);
    }

    protected virtual void Update()
    {
        float now = Time.unscaledTime;

        // transient/변주 표정을 보여준 뒤 idle로 복귀하는 예약 처리.
        if (_revertToIdleTime > 0f && now >= _revertToIdleTime)
        {
            _revertToIdleTime = 0f;
            GoIdle();
            return;
        }

        if (!_inIdle || _inIdleVariation) return;

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

    // ─────────────────────────────────────────────────────────────
    // 스테이지 구성 (카메라 + 라이트 + 캐릭터를 전용 레이어에 코드로 생성)
    // ─────────────────────────────────────────────────────────────

    private void BuildStage()
    {
        if (characterPrefab == null)
        {
            // 캐릭터 프리팹(예: 유료 에셋 Rob13)이 비어 있으면 스테이지를 만들지 않고 조용히 비활성화한다.
            // 유료 캐릭터 에셋은 라이선스상 공개 저장소에서 제외돼 있어, 에셋 없이 클론한 경우 이 경로를 탄다.
            // 이 컴포넌트만 꺼질 뿐 앱 전체 동작에는 영향이 없다.
            // 직접 만든 캐릭터를 연결하는 방법은 Docs/CUSTOM_CHARACTER.md 참고.
            Debug.LogWarning("[CharacterStage] characterPrefab이 지정되지 않아 캐릭터 렌더를 건너뛴다(앱 동작에는 영향 없음). " +
                             "커스텀 캐릭터 연결법은 Docs/CUSTOM_CHARACTER.md 참고.");
            enabled = false;
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

        // 스테이지 루트 — UI/씬 카메라와 멀리 떨어뜨리고, 인스턴스마다 X로 더 띄워 서로 겹치지 않게 한다.
        _stageRoot = new GameObject("CharacterStage(Runtime)");
        _stageRoot.transform.position = new Vector3(5000f + _stageCount * 100f, 5000f, 5000f);
        _stageCount++;

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
        var stageLight = lightGo.AddComponent<Light>();
        stageLight.type = LightType.Directional;
        stageLight.color = lightColor;
        stageLight.intensity = lightIntensity;
        stageLight.cullingMask = 1 << _stageLayer;

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
    // 재생 API (이 클래스 자체 + 하위 클래스 공용)
    // ─────────────────────────────────────────────────────────────

    /// <summary>idle 루프로 진입하고 다음 변주를 예약. idleState가 비면 아무 것도 재생하지 않는다.</summary>
    protected void GoIdle()
    {
        _inIdle = true;
        _inIdleVariation = false;
        _revertToIdleTime = 0f;
        Play(idleState);
        ScheduleNextIdleVariation();
    }

    /// <summary>state를 재생하고 holdSeconds 뒤 idle로 복귀(인사/표정 등 일시 모션).</summary>
    protected void PlayTransient(string state, float holdSeconds)
    {
        _inIdle = false;
        _inIdleVariation = false;
        Play(state);
        _revertToIdleTime = Time.unscaledTime + Mathf.Max(0.1f, holdSeconds);
    }

    /// <summary>state를 재생하고 그대로 유지한다(종료 연출 등 — idle로 자동 복귀하지 않음).</summary>
    protected void PlayPersistent(string state)
    {
        _inIdle = false;
        _inIdleVariation = false;
        _revertToIdleTime = 0f;
        Play(state);
    }

    /// <summary>풀에서 빈 항목이 아닌 state 하나를 랜덤 선택. 풀이 비면 null.</summary>
    protected static string PickRandom(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }

    /// <summary>Animator state를 CrossFade로 재생. state명이 없으면 무시.</summary>
    protected void Play(string stateName)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName)) return;
        if (logEvents) Debug.Log($"[CharacterStage] play: {stateName}");
        _animator.CrossFadeInFixedTime(stateName, crossFadeSeconds);
    }

    /// <summary>다음 idle 변주 시각을 min~max 사이 랜덤으로 잡는다.</summary>
    private void ScheduleNextIdleVariation()
    {
        float min = Mathf.Min(idleVariationMinInterval, idleVariationMaxInterval);
        float max = Mathf.Max(idleVariationMinInterval, idleVariationMaxInterval);
        _nextIdleVarTime = Time.unscaledTime + Random.Range(min, max);
    }
}
