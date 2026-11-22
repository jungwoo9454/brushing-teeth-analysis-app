using System;
using UnityEngine;

/// <summary>
/// 버디 보상 스티커 1종의 정의(카탈로그 항목). 디자이너가 StickerCatalog 에셋에 채운다.
/// </summary>
[Serializable]
public class StickerDef
{
    [Tooltip("고유 id — 획득 기록의 키. 비우면 안 되고 중복도 안 된다.")]
    public string id;

    [Tooltip("표시 이름.")]
    public string displayName;

    [Tooltip("스티커 이미지(UI용).")]
    public Sprite sprite;

    [TextArea] public string description;
}

/// <summary>
/// 버디 스티커 카탈로그 — 해금 가능한 전체 스티커 목록(보상 풀).
/// 생성: 프로젝트 창에서 Create &gt; 세살버릇 &gt; Sticker Catalog.
/// 보통 Assets/Resources/StickerCatalog.asset 로 두면 StickerMng가 자동 로드한다
/// (코드에서 StickerMng.ins.SetCatalog(...)로 주입도 가능).
///
/// 스티커 목록은 나중에 채워 넣으면 되고, 비어 있는 동안에는 해금이 조용히 건너뛰어진다.
/// </summary>
[CreateAssetMenu(fileName = "StickerCatalog", menuName = "세살버릇/Sticker Catalog")]
public class StickerCatalog : ScriptableObject
{
    [Tooltip("해금 가능한 전체 스티커. 양치 완료 시 아직 없는 것 중에서 랜덤 1개가 해금된다.")]
    public StickerDef[] stickers = Array.Empty<StickerDef>();
}

/// <summary>스티커가 새로 해금됐을 때 EventBus로 발행(추후 획득 연출/팝업 UI가 구독).</summary>
public struct StickerUnlockedEvent
{
    public string childId;
    public string stickerId;
    public StickerDef sticker;
}
