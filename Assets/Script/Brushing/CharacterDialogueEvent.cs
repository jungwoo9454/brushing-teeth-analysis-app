/// <summary>
/// 하단 캐릭터 대사창에 띄울 한 줄을 전달하는 이벤트.
/// DialogueDirector가 루틴 상황에 맞춰 발행하고, 대사창 UI가 구독해 표시한다.
/// (대사창 UI는 다음 단계에서 구현 — 지금은 이벤트만 발행되며 logEvents로 검증 가능)
/// </summary>
public struct CharacterDialogueEvent
{
    /// <summary>화면에 표시할 대사 문구(이미 구역명이 치환된 최종 문자열).</summary>
    public string text;

    /// <summary>대사의 성격 — UI가 말풍선 색/캐릭터 표정을 분기하는 데 쓸 수 있다.</summary>
    public DialogueKind kind;

    /// <summary>이 대사가 가리키는 구역 (hasZone=true일 때만 유효). 구역 힌트 이미지 등에 사용.</summary>
    public UiZone zone;

    /// <summary>zone 필드가 유효한지. 구역과 무관한 멘트(완료/칭찬 등)는 false.</summary>
    public bool hasZone;
}

/// <summary>대사 성격 분류.</summary>
public enum DialogueKind
{
    /// <summary>"이제 OO를 닦아주세요" — 새 구역 안내.</summary>
    Guide,

    /// <summary>"OO를 조금 더 닦아주세요" — 시간이 부족한 채 넘어갈 때.</summary>
    Encourage,

    /// <summary>"거기 말고 OO를 닦아요" — 다른 구역을 닦고 있을 때.</summary>
    Correct,

    /// <summary>"양치를 하고 있지 않아요" — 한동안 양치를 멈췄을 때.</summary>
    Idle,

    /// <summary>"잘했어요!" — 칭찬/완료.</summary>
    Praise,
}
