using System;
using UnityEngine;

/// <summary>
/// 양치 순차 가이드의 "구역 순서 + 구역별 배분 시간"을 담는 데이터 에셋.
/// 코드 수정·빌드 없이 인스펙터에서 순서/시간을 바꿀 수 있도록 ScriptableObject로 정의한다.
/// (Project 창에서 우클릭 → Create → 세살버릇 → Brushing Routine 으로 .asset 생성)
///
/// BrushingRoutineRunner가 이 데이터를 읽어 순차 진행을 구동한다.
/// 에셋을 지정하지 않으면 Runner가 CreateDefault()의 기본 루틴으로 폴백한다.
/// </summary>
[CreateAssetMenu(menuName = "세살버릇/Brushing Routine", fileName = "BrushingRoutine")]
public class BrushingRoutine : ScriptableObject
{
    /// <summary>한 단계 = 닦을 구역 하나 + 배분 시간.</summary>
    [Serializable]
    public class Step
    {
        [Tooltip("이 단계에서 닦을 5-zone 구역.")]
        public UiZone zone;

        [Tooltip("이 구역에 배분할 시간(초). 0 이하면 totalSeconds를 균등 분배한 몫을 자동 사용.")]
        [Min(0f)] public float seconds = 0f;
    }

    [Tooltip("전체 양치 목표 시간(초). 기본 180초 = 3분.")]
    [SerializeField, Min(0f)] private float totalSeconds = 180f;

    [Tooltip("닦는 순서. 위에서 아래로 진행한다. 같은 구역을 여러 번 넣어도 된다. " +
             "★ 순서/시간은 여기서 자유롭게 수정 — 임의 기본값은 앞니→좌상→좌하→우상→우하.")]
    [SerializeField]
    private Step[] steps =
    {
        new Step { zone = UiZone.Front },
        new Step { zone = UiZone.UpperLeft },
        new Step { zone = UiZone.LowerLeft },
        new Step { zone = UiZone.UpperRight },
        new Step { zone = UiZone.LowerRight },
    };

    /// <summary>전체 목표 시간(초).</summary>
    public float TotalSeconds => totalSeconds;

    /// <summary>단계 수.</summary>
    public int StepCount => steps != null ? steps.Length : 0;

    /// <summary>index 단계의 대상 구역.</summary>
    public UiZone ZoneAt(int index) => steps[index].zone;

    /// <summary>
    /// index 단계에 배분된 시간(초).
    /// seconds가 명시(>0)면 그 값, 아니면 "명시되지 않은 단계들"이 남는 시간을 균등 분배한 몫.
    /// 예) 5단계 모두 미지정 + total 180 → 각 36초.
    /// </summary>
    public float SecondsAt(int index)
    {
        float explicitSum = 0f;
        int autoCount = 0;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].seconds > 0f) explicitSum += steps[i].seconds;
            else autoCount++;
        }

        if (steps[index].seconds > 0f) return steps[index].seconds;

        float leftover = Mathf.Max(0f, totalSeconds - explicitSum);
        return autoCount > 0 ? leftover / autoCount : 0f;
    }

    /// <summary>
    /// 인스펙터 에셋 없이 코드에서 바로 쓸 수 있는 기본 루틴(앞니→좌상→좌하→우상→우하, 3분 균등).
    /// Runner가 routine 미지정 시 폴백으로 사용. 런타임 전용 인스턴스라 디스크에 저장되지 않는다.
    /// </summary>
    public static BrushingRoutine CreateDefault()
    {
        // CreateInstance 시 위 필드 초기값(steps/totalSeconds)이 그대로 적용된다.
        return CreateInstance<BrushingRoutine>();
    }
}
