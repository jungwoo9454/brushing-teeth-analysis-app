using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오브젝트가 활성화될 때(LayoutGroup/ContentSizeFitter가 붙은 컨테이너) 레이아웃을 강제로 다시 계산한다.
///
/// 배경: 탭 전환처럼 비활성 → 활성으로 바뀌는 화면은 Unity가 레이아웃을 자동으로 즉시
/// 다시 계산하지 않을 때가 있다. 그러면 VerticalLayoutGroup이 자식 위치를 갱신하지 못해
/// 섹션끼리(예: 종합 리포트 중앙 섹션과 Break Days) 겹쳐 보인다.
///
/// 해결: OnEnable에서 즉시 한 번 강제 리빌드하고, 자식 ContentSizeFitter가 크기를 정한 뒤
/// 한 프레임 더 기다렸다가 한 번 더 리빌드한다(중첩 레이아웃까지 안정적으로 정렬).
///
/// 부착 위치: 정렬이 깨지는 LayoutGroup 컨테이너(여기선 BrushingDetailReport의 RealSpace).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class LayoutRebuildOnEnable : MonoBehaviour
{
    [Tooltip("활성화 후 추가로 한 프레임 뒤 한 번 더 리빌드(자식 ContentSizeFitter 반영).")]
    [SerializeField] private bool rebuildNextFrame = true;

    private RectTransform _rt;

    private void Awake() => _rt = (RectTransform)transform;

    private void OnEnable()
    {
        Rebuild();
        if (rebuildNextFrame && isActiveAndEnabled)
            StartCoroutine(RebuildNextFrame());
    }

    private IEnumerator RebuildNextFrame()
    {
        // 한 프레임 대기 — 자식 ContentSizeFitter가 이번 프레임에 크기를 정한 뒤 다시 정렬.
        yield return null;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_rt == null) _rt = (RectTransform)transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
    }
}
