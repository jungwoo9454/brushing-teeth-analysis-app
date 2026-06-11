using UnityEngine;

/// <summary>
/// 양치 결과(BrushingFinish) 화면의 캐릭터. 베이스 <see cref="CharacterStage"/>가 3D 캐릭터를
/// 전용 카메라/RenderTexture로 렌더해 이 오브젝트의 RawImage에 합성한다(메인 메뉴·양치 씬과 동일).
///
/// 결과 점수에 따라 표정을 고정한다:
///   - 높은 점수: 기쁜 표정 / 보통: 평범 / 낮음: 안 좋은 표정.
/// BrushingFinishView가 결과를 바인딩할 때 <see cref="ShowForScore"/>를 호출한다.
///
/// 캐릭터 프리팹이 없으면(유료 에셋 제외 빌드) 베이스가 조용히 비활성화 — 앱 동작엔 영향 없음.
/// </summary>
[DisallowMultipleComponent]
public class BrushingFinishCharacter : CharacterStage
{
    [Header("점수별 표정 (Animator state명)")]
    [Tooltip("높은 점수일 때 — 기쁜 표정.")]
    [SerializeField] private string happyState = "Laught";
    [Tooltip("보통 점수일 때 — 평범한 표정.")]
    [SerializeField] private string neutralState = "EmoTalk1";
    [Tooltip("낮은 점수일 때 — 안 좋은 표정.")]
    [SerializeField] private string badState = "Cry";

    [Header("점수 구간")]
    [Tooltip("이 점수 이상이면 기쁜 표정.")]
    [SerializeField] private int happyThreshold = 70;
    [Tooltip("이 점수 이상이면 평범, 미만이면 안 좋은 표정.")]
    [SerializeField] private int normalThreshold = 40;

    /// <summary>점수에 맞는 표정을 골라 고정 재생한다(idle 변주 없이 유지).</summary>
    public void ShowForScore(int score)
    {
        string state = score >= happyThreshold ? happyState
                     : score >= normalThreshold ? neutralState
                     : badState;
        PlayPersistent(state);
    }
}
