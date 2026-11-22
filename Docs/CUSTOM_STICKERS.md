# 커스텀 스티커(버디 보상) 적용 가이드

세살버릇의 **버디(Buddy) 보상 시스템**은 아이가 양치를 완료할 때마다 **랜덤 스티커 1개**를 해금해
모으는 재미를 줍니다. 스티커는 **아이 프로필마다 따로** 관리됩니다.

원본 프로젝트는 유료 아이콘 팩을 스티커로 사용했지만, **라이선스상 이 에셋은 공개 저장소에 포함할 수
없어 제외**되어 있습니다(`Assets/Texture/Icons/` 는 `.gitignore` 처리).

> 스티커가 하나도 없어도 앱은 정상 동작합니다 — 양치 완료 시 해금만 조용히 건너뜁니다.
> 아래 절차로 직접 만든(혹은 보유한) 아이콘을 넣으면 그때부터 해금이 시작됩니다.

---

## 1. 동작 구조

| 요소 | 역할 |
|---|---|
| `StickerCatalog` (ScriptableObject) | 해금 가능한 **전체 스티커 목록**(보상 풀). `StickerDef[]`. |
| `StickerMng` (싱글톤) | 양치 완료(`BrushingSessionCompletedEvent`)마다 **활성 아이에게 미획득 스티커 1개를 랜덤 해금**. |
| `ChildProfile.earnedStickerIds` | 아이별 **획득한 스티커 id** 목록(로컬 저장). |

- `StickerDef` 1개 = `id`(고유 키) + `displayName` + `sprite`(이미지) + `description`.
- 해금 기록은 아이 프로필에 저장되어 **아이마다 모은 스티커가 다릅니다**.
- 카탈로그는 `Assets/Resources/StickerCatalog.asset` 을 `StickerMng`가 **자동 로드**합니다
  (코드에서 `StickerMng.ins.SetCatalog(...)`로 주입도 가능).

---

## 2. 스티커 이미지 준비

1. 스티커로 쓸 이미지를 프로젝트에 임포트합니다(원본과 같은 위치를 쓰려면 `Assets/Texture/Icons/`
   폴더 — 이 폴더는 `.gitignore`라 다시 커밋되지 않습니다).
2. 각 이미지의 **Texture Type**을 `Sprite (2D and UI)` 로 설정합니다.

---

## 3. 카탈로그 만들기 / 채우기

이미 비어 있는 `Assets/Resources/StickerCatalog.asset` 이 들어 있습니다. 없으면 새로 만듭니다:

- 프로젝트 창에서 **Create → 세살버릇 → Sticker Catalog**
- 파일명을 **`StickerCatalog`** 로 하고 **`Assets/Resources/`** 아래에 둡니다(자동 로드 경로).

카탈로그를 선택해 인스펙터에서 **Stickers** 배열에 항목을 추가합니다. 항목마다:

| 필드 | 설명 |
|---|---|
| `id` | **고유 문자열**(중복 금지). 획득 기록의 키 — 한번 정하면 바꾸지 마세요. 예: `star_01` |
| `displayName` | 표시 이름. 예: "반짝별" |
| `sprite` | 2번에서 준비한 스티커 이미지 |
| `description` | (선택) 설명 |

> `id`는 저장의 키이므로 **이미 배포된 뒤에는 바꾸지 마세요**(바꾸면 기존 획득 기록과 매칭이 깨집니다).
> 새 스티커는 **추가**만 하면 안전합니다.

---

## 4. 확인

- 카탈로그에 스티커를 1개 이상 넣고 플레이해 양치를 **완료**하면, 활성 아이에게 스티커가 하나
  해금되고 `StickerUnlockedEvent`가 발행됩니다(획득 연출/팝업 UI에서 구독).
- 모든 스티커를 다 모으면 더 이상 해금되지 않습니다.

---

## 5. UI에서 획득/미획득 표시 (추후)

스티커 UI를 만들 때 `StickerMng`의 조회 API를 사용하세요.

```csharp
var child = UserMng.ins.ActiveChild;
var all     = StickerMng.ins.AllStickers();      // 전체 스티커
var earned  = StickerMng.ins.GetEarned(child);   // 이 아이가 획득한 것
var locked  = StickerMng.ins.GetLocked(child);   // 아직 못 얻은 것
bool has    = StickerMng.ins.IsEarned(child, "star_01");
```

- 보통 **전체 그리드**를 그리고, `IsEarned`로 획득한 것은 컬러로, 미획득은 실루엣/잠금으로 표시합니다.
- 양치 완료 화면(BrushingFinish)의 `Slot/Reward` 자리에 방금 해금한 스티커를 띄우면 좋습니다.

---

## 6. 스티커 없이 운영

카탈로그를 비워 두면 해금이 일어나지 않을 뿐 다른 기능에는 영향이 없습니다.
나중에 아이콘이 준비되면 카탈로그만 채우면 바로 동작합니다.
