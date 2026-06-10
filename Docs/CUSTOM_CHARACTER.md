# 커스텀 캐릭터 적용 가이드

세살버릇은 메뉴·결과·양치 화면에 3D 캐릭터를 띄워 아이의 양치를 응원합니다.
원본 프로젝트는 유료 에셋(Rob13 로봇)을 사용했지만, **라이선스상 이 에셋은 공개
저장소에 포함할 수 없어 제외**되어 있습니다.

> 캐릭터 에셋이 없어도 앱은 정상적으로 빌드·실행됩니다. 캐릭터를 그리는
> `CharacterStage` 컴포넌트가 자동으로 비활성화될 뿐, 다른 기능(양치 분석·점수·리포트)은
> 그대로 동작합니다.

이 문서는 **직접 만든(혹은 보유한) 캐릭터를 연결하는 방법**을 설명합니다.

---

## 1. 캐릭터 렌더링 구조

이 프로젝트는 URP **2D** Renderer를 사용하므로 3D 메시를 월드에 직접 합성하기 어렵습니다.
그래서 캐릭터를 별도 레이어/카메라로 **오프스크린(RenderTexture) 렌더**한 뒤, 2D UI의
`RawImage`에 얹는 방식을 씁니다.

핵심 스크립트는 두 개입니다 (`Assets/Script/Brushing/`).

| 스크립트 | 역할 |
|---|---|
| `CharacterStage` | 전용 카메라·라이트·캐릭터 인스턴스를 코드로 만들어 RenderTexture에 렌더. idle 루프와 주기적 표정 변주를 돌린다. **메뉴·결과 화면**처럼 "가끔 표정 짓는" 화면에 그대로 부착. |
| `CharacterStagePresenter` | `CharacterStage`를 상속. **양치 흐름 이벤트**(시작/대사/종료)에 맞춰 모션을 전환한다. 양치 씬의 RawImage에 부착. |

캐릭터는 **인스펙터의 `characterPrefab` 필드**(GUID 참조)로만 연결됩니다. 즉, 앱 C# 코드는
특정 캐릭터 에셋에 컴파일타임으로 의존하지 않으며, 프리팹만 갈아끼우면 됩니다.

---

## 2. 준비물 — 캐릭터 프리팹 요구사항

연결할 캐릭터 프리팹은 다음을 만족해야 합니다.

1. **3D 모델 프리팹**(메시 + 머티리얼). 정면을 바라보도록 배치합니다(기본값은 Y축 180° 회전).
2. 루트 또는 자식에 **`Animator` 컴포넌트**와 **Animator Controller**가 있어야 합니다.
3. Animator Controller에 아래 **state 이름들**이 존재해야 합니다. `CharacterStage`는 파라미터/
   트랜지션 없이 state 이름을 **`CrossFade`로 직접 재생**하기 때문입니다.

### 필요한 Animator state 이름

| 용도 | 기본 state 이름 | 인스펙터 필드 |
|---|---|---|
| 평상시 idle | `EmoTalk1` | `CharacterStage > idleState` |
| idle 중 랜덤 표정 변주 | `Laught`, `ThumbsUp`, `Win`, `Hello` | `CharacterStage > idleVariations` |
| 양치 시작 인사 | `Hello` | `Presenter > startState` |
| 칭찬(긍정) 모션 | `ThumbsUp`, `Laught`, `Win` | `Presenter > goodStates` |
| 부족·교정·미양치(부정) 모션 | `No`, `Cry`, `Emo Angry` | `Presenter > badStates` |
| 세션 성공 종료 | `Win` | `Presenter > finishSuccessState` |
| 세션 미흡 종료 | `DontKnow` | `Presenter > finishIncompleteState` |

> **두 가지 선택지**
> - (A) 위 이름 그대로 Animator Controller에 state를 만든다 → 인스펙터를 건드릴 필요 없음.
> - (B) 내 컨트롤러의 state 이름에 맞춰 위 **인스펙터 필드 값을 바꾼다**.
>
> 정면을 보지 않는(몸을 돌리거나 이동하는) 모션은 idle 변주에서 빼는 것을 권장합니다 —
> RenderTexture 카메라가 정면 고정이라 옆모습이 어색하게 보일 수 있습니다.

---

## 3. 적용 절차

### 3-1. 캐릭터 레이어 추가 (권장)

`Project Settings → Tags and Layers`에서 **`CharacterStage`** 라는 이름의 레이어를 하나
추가합니다. 캐릭터는 이 레이어로만 렌더되어 다른 콘텐츠와 분리됩니다.

> 레이어가 없으면 `Default`로 폴백되며, 다른 UI/씬 오브젝트와 겹쳐 보일 수 있습니다.

### 3-2. 프리팹을 폴더에 배치

내 캐릭터 에셋을 프로젝트에 임포트합니다. 위치는 자유지만, 원본과 동일하게 두려면
`Assets/Character/` 폴더를 만들어 그 안에 둡니다(이 폴더는 `.gitignore`에 등록돼 있어
다시 커밋되지 않습니다).

### 3-3. 컴포넌트에 프리팹 연결

캐릭터를 띄울 화면을 엽니다. 캐릭터 슬롯은 `RawImage`가 있는 오브젝트에 `CharacterStage`
또는 `CharacterStagePresenter` 컴포넌트가 붙어 있는 형태입니다.

- 씬에서 해당 컴포넌트를 가진 오브젝트를 찾습니다.
  (Hierarchy 검색창에 `t:CharacterStage` 입력)
- 인스펙터의 **`Character Prefab`** 필드에 내 캐릭터 프리팹을 드래그합니다.
- 필요하면 아래 값으로 위치/크기/카메라를 조정합니다.
  - `Character Local Position` / `Character Euler Angles` / `Character Scale`
  - `Camera Local Position` / `Camera Pitch` / `Camera Fov`

캐릭터가 사용되는 대표 화면:
- **메뉴/결과 화면** — `CharacterStage` (idle + 표정 변주만).
- **양치 화면** — `CharacterStagePresenter` (양치 이벤트에 반응).

### 3-4. 확인

플레이 모드로 들어가 캐릭터가 UI 슬롯 안에 정면으로 보이고, idle 중 가끔 표정을 바꾸면
정상입니다. 양치 화면에서는 시작 시 인사(Hello), 칭찬/지적에 따라 표정이 바뀌어야 합니다.

---

## 4. 자주 겪는 문제

| 증상 | 원인 / 해결 |
|---|---|
| 콘솔에 `characterPrefab이 지정되지 않아…` 경고 | 정상. 프리팹을 연결하지 않은 상태. 위 3-3 수행. |
| 캐릭터가 안 보임 | `CharacterStage` 레이어 누락(3-1) 또는 카메라 위치/스케일 문제. `Camera Local Position`의 z를 더 음수로. |
| 애니메이션이 안 바뀜 | Animator Controller의 state 이름이 표(2장)와 불일치. 이름을 맞추거나 인스펙터 필드를 수정. |
| 캐릭터가 다른 UI와 겹쳐 보임 | 레이어를 `Default`로 폴백 중. `CharacterStage` 레이어를 추가하고 캐릭터에 지정. |
| 배경이 불투명하게 가림 | `Transparent Background` 체크 확인(기본 ON). |

---

## 5. 캐릭터를 아예 빼고 싶다면

캐릭터 없이 운영해도 무방합니다. `characterPrefab`을 비워 두면 `CharacterStage`가 스스로
비활성화되어 빈 슬롯으로 남습니다. UI에서 슬롯 자체를 숨기려면 해당 `RawImage` 오브젝트를
비활성화하세요.
