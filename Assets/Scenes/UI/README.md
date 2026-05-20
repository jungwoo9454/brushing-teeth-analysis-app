# Assets/Scenes/UI — UI 레이아웃 스캐폴드

세살버릇 앱의 UI 레이아웃 디자인을 Unity로 옮긴 스캐폴드입니다.
디자인 시안은 Claude Design(`Prototype.html`)을 기준으로 1:1 매핑했습니다.

## 폴더 구조

```
Assets/Scenes/UI/
├── Tokens/            디자인 토큰 (색·타이포·라운드 등)
│   └── tokens.uss     라이트/다크 CSS variables
├── Common/            공통 컴포넌트 스타일
│   └── common.uss     카드/버튼/칩/탭바/카메라뷰 등
├── Kid/               아이 모드 화면 (UI Toolkit)
│   ├── Home.uxml
│   ├── Finish.uxml
│   ├── ChildReport.uxml
│   └── Reward.uxml
├── Parent/            보호자 모드 화면 (UI Toolkit)
│   ├── PinUnlock.uxml
│   ├── Dashboard.uxml
│   ├── WeeklyReport.uxml
│   ├── Settings.uxml
│   └── Notifications.uxml
├── Onboard/           온보딩 9화면 (UI Toolkit)
│   ├── Splash.uxml
│   ├── Signup.uxml
│   ├── Terms.uxml
│   ├── ParentProfile.uxml
│   ├── PinSetup.uxml
│   ├── ChildCreate.uxml
│   ├── AddMore.uxml
│   ├── Permission.uxml
│   └── Tutorial.uxml
├── Account/           가족 계정 5화면 (UI Toolkit)
│   ├── ProfileSelect.uxml
│   ├── ManageProfiles.uxml
│   ├── AddParent.uxml
│   ├── CodeEntry.uxml
│   └── JoinConfirm.uxml
└── Brushing/          양치 중 화면 (UGUI — 프로그래매틱)
    └── README.md      → BrushingScreenBuilder.cs 사용법
```

## 아키텍처 — 왜 하이브리드?

- **UI Toolkit (UXML/USS)**: 정적 메뉴/리포트/온보딩. 시안의 USS 토큰과 직결되고, .unity 머지 충돌이 없다.
- **UGUI (프로그래매틱)**: 양치 중 화면. 카메라 RawImage + AR 박테리아 오버레이 + ML 추적 도트가 합쳐지는 화면이라 UGUI가 자연스럽다.

분리 기준: 카메라 피드 위 동적 오버레이가 필요한가? → 그렇다면 UGUI. 아니라면 UI Toolkit.

## 테마 (라이트/다크)

각 UXML 루트의 클래스를 `theme-light` ↔ `theme-dark`로 바꾸면 토큰이 알아서 색을 갈아준다.
런타임에서는 `ThemeManager.ins.Toggle()`이 처리한다.

## 라우터 사용법

```csharp
using Sesalbeoreut.UI;

// 화면 전환
UIRouter.ins.Show("Kid/Home");
UIRouter.ins.Show("Parent/PinUnlock");

// 뒤로 가기
UIRouter.ins.GoBack();

// 테마 토글
ThemeManager.ins.Toggle();
```

### UXML 동적 로드 — Resources 폴더 안내
`UIRouter.Show()`는 `Resources.Load<VisualTreeAsset>("UI/" + path)`로 UXML을 가져옵니다.
지금 UXML은 `Assets/Scenes/UI/...`에 있으니 두 가지 중 하나가 필요합니다:

1. **`Assets/Resources/UI/`에 복사/이동** — Unity 표준. 빌드에 자동 포함.
2. **인스펙터 슬롯으로 교체** — `UIRouter`에 `VisualTreeAsset[]` 필드를 추가해 직접 할당 (Resources 폴더 안 써도 됨)

스캐폴드 단계라 선택은 후속 PR에서 진행. 시연만 할 거면 `BrushingScreenBuilder`처럼 컴포넌트를 직접 씬에 붙여도 충분합니다.

## 시안과 다른 점 (의도된 단순화)

- **둥근 모서리·그림자**: USS는 `border-radius`/`background-color`까지만 직접 지원. 그림자(`box-shadow`)는 9-slice 스프라이트나 셰이더로 대체 필요 — 현재는 단색 카드로 처리.
- **아이콘**: 디자인의 SVG 아이콘은 임시로 이모지(🏠/📊/🏆/🔒 등)로 대체. 후속 PR에서 SVG/스프라이트 자원으로 교체.
- **캐릭터(버디) SVG**: 임시로 🐻 이모지 + 색 배경 원. 후속 PR에서 일러스트 자원으로 교체.
- **폰트**: Pretendard는 `Resources/Font/`에 폰트 자원을 추가하면 USS의 `-unity-font-definition`이 자동 적용. 현재는 Unity 기본 폰트.
- **PIN 도트 활성/비활성**: 4개 모두 활성으로 표기. 런타임 상태와 연동은 컨트롤러 추가 후.

## 디자인 시안 출처

`Prototype.html` 핸드오프 패키지 (Claude Design). 핵심 참조:
- `hifi/tokens.css` → `Tokens/tokens.uss`
- `hifi/components.jsx` → `Common/common.uss` + 각 UXML
- `hifi/screens.jsx` → `Kid/*.uxml`
- `hifi/screens-parent.jsx` → `Parent/*.uxml`
- `hifi/screens-onboard.jsx` → `Onboard/*.uxml`
- `hifi/screens-account.jsx` → `Account/*.uxml`
