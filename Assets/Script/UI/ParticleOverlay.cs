using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 파티클을 전용 레이어/카메라로 RenderTexture에 오프스크린 렌더해 2D UI(RawImage)에 합성한다.
/// 이 프로젝트는 모든 UI가 Screen Space - Overlay라 월드 파티클이 카메라 영상/UI 뒤에 가려진다 —
/// 그래서 CharacterStage와 동일하게 "전용 카메라 → RT → RawImage" 방식으로 UI 위에 올린다.
///
/// 전체화면 RawImage 오브젝트(오버레이 캔버스의 최상단 자식)에 부착하고 particlePrefab을 지정하면,
/// 그 파티클이 화면 전체에 합성된다. orthographicSize는 메인 카메라와 맞춰 월드 좌표가 일치하게 한다.
///
/// 사용처:
///   - 양치 버블(BrushingBubbles): 프리팹의 BrushingBubblesController가 '양치 중'에만 emit.
///   - 스티커/완료 폭죽(ConfettiBurst): playOnAwake 일회성 — 화면이 켜질 때 터진다.
/// </summary>
[RequireComponent(typeof(RawImage))]
[DisallowMultipleComponent]
public class ParticleOverlay : MonoBehaviour
{
    [Header("합성할 파티클")]
    [Tooltip("오프스크린에서 렌더할 파티클 프리팹(ParticleSystem 루트).")]
    [SerializeField] private GameObject particlePrefab;

    [Tooltip("이 파티클만 렌더할 전용 레이어 이름(없으면 Default로 폴백 — 다른 콘텐츠와 섞일 수 있음).")]
    [SerializeField] private string stageLayer = "ParticleStage";

    [Header("렌더 설정")]
    [Tooltip("RT 해상도. 화면 비율(세로형 1:2)에 맞춘다.")]
    [SerializeField] private int rtWidth = 1080;
    [SerializeField] private int rtHeight = 2160;
    [Tooltip("스테이지 카메라 orthographicSize — 메인 카메라와 같게(월드 좌표 일치).")]
    [SerializeField] private float orthographicSize = 5f;
    [Tooltip("파티클 에미터의 카메라 기준 위치. 버블은 하단(y 음수), 폭죽은 중앙(0).")]
    [SerializeField] private Vector3 emitterLocalPosition = new Vector3(0f, -5.2f, 0f);

    [Header("디버그/테스트")]
    [Tooltip("켜면 파티클의 자체 게이팅(예: 양치 중에만)을 무시하고 항상 방출한다 — 렌더 테스트용.")]
    [SerializeField] private bool forceEmit = false;

    private RawImage _img;
    private RenderTexture _rt;
    private Camera _cam;
    private GameObject _root;

    // 여러 오버레이가 같은 좌표에 겹쳐 서로의 카메라에 잡히지 않게 인스턴스마다 멀리 띄운다.
    private static int _count;

    private void Awake()
    {
        _img = GetComponent<RawImage>();
        if (particlePrefab == null)
        {
            Debug.LogWarning("[ParticleOverlay] particlePrefab 미지정 — 합성을 건너뛴다.");
            enabled = false;
            return;
        }

        int layer = LayerMask.NameToLayer(stageLayer);
        if (layer < 0)
        {
            Debug.LogWarning($"[ParticleOverlay] '{stageLayer}' 레이어가 없어 Default로 폴백한다 — 다른 콘텐츠와 섞일 수 있다.");
            layer = 0;
        }

        // RT(알파 포함) → RawImage.
        // ⚠ 깊이 버퍼(24)가 없으면 URP 2D 렌더러가 "Fake or uninitialized surface"로 렌더패스를 못 열어
        //   RT가 매 프레임 클리어되지 않는다(잔상/트레일). CharacterStage와 동일하게 24비트 + AA로 만든다.
        _rt = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32) { name = "ParticleOverlayRT", antiAliasing = 2 };
        _rt.Create();
        _img.texture = _rt;
        _img.color = Color.white;

        // 오프스크린 스테이지 — 메인 씬과 멀리 떨어뜨리고 인스턴스마다 더 띄운다.
        _root = new GameObject("ParticleOverlay(Runtime)");
        _root.transform.position = new Vector3(7000f + _count * 100f, 7000f, 0f);
        _count++;

        // 스테이지 카메라 — 이 레이어만, 투명 배경, RT로 출력.
        var camGo = new GameObject("OverlayCamera");
        camGo.transform.SetParent(_root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        _cam = camGo.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = orthographicSize;
        _cam.cullingMask = 1 << layer;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 50f;
        _cam.targetTexture = _rt;
        _cam.allowHDR = false;
        _cam.allowMSAA = true;

        // 파티클 인스턴스 — 스테이지 안, 전용 레이어, 카메라 기준 지정 위치.
        var p = Instantiate(particlePrefab, _root.transform);
        p.transform.localPosition = emitterLocalPosition;
        SetLayerRecursive(p, layer);

        // 테스트 모드 — 파티클 자체 게이팅(BrushingBubblesController 등)을 끄고 항상 방출.
        if (forceEmit)
        {
            var ctrl = p.GetComponent<BrushingBubblesController>();
            if (ctrl != null) ctrl.enabled = false;
            var ps = p.GetComponent<ParticleSystem>();
            if (ps != null) { var em = ps.emission; em.enabled = true; if (!ps.isPlaying) ps.Play(); }
        }
    }

    private void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (_root != null) Destroy(_root);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
