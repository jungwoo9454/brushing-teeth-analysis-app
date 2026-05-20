# 양치 중 화면 (UGUI)

이 폴더는 양치 중 화면의 **UGUI 레이아웃**을 다룹니다. (다른 정적 화면은 모두 UI Toolkit/UXML)

## 왜 UGUI인가
양치 중 화면은 **카메라 피드 + AR 박테리아 오버레이 + 입/칫솔 추적 오버레이**가 합쳐지는 화면이라, 카메라 RawImage 위로 자유롭게 RectTransform을 깔 수 있는 UGUI가 가장 자연스럽습니다.

## 사용법
1. `Assets/Scenes/Main.unity` 씬을 엽니다.
2. 빈 GameObject(예: `BrushingUI`)를 만들고 `BrushingScreenBuilder` 컴포넌트를 추가합니다.
3. Play하면 Awake에서 전체 레이아웃이 자동 생성됩니다. (Canvas가 없으면 함께 만듭니다)
4. 런타임 갱신(타이머/구역/트래커 좌표/카메라 피드)은 `BrushingScreenController`(후속 작업)가 담당합니다.

## 레이아웃 (디자인 참조: design/hifi/screens.jsx HiBrush)
- **상단**: 타이머 `0:48 / 2:00` + "다음 닦을 곳" 칩 + 일시정지 + 닫기
- **진행률 바**: 프라이머리 색상
- **카메라 뷰 (250px)**: 입 영역 점선 박스 + 칫솔 트래커 도트 + 좌상단 "AI 분석 중" + 하단 "현재 구역" 라벨
- **하단**: 캐릭터(120px) + 말풍선 ("지금 닦을 곳: 위 오른쪽 어금니")

## 외부 참조 슬롯
`BrushingScreenBuilder`의 public 필드를 통해 ML 파이프라인과 연결:
- `cameraFeed` (RawImage) — WebCamTexture 주입
- `mouthBox` (RectTransform) — MediaPipe 입 폴리곤에서 갱신
- `tracker` (RectTransform) — YOLO 칫솔 keypoint(swap 적용: `keypoints[1]` head_center)에서 갱신
- `timerText`, `nextZoneText`, `currentZoneText`, `bubbleZoneText`, `progressFill`, `character`

## TODO (후속 PR)
- 둥근 모서리 9-slice 스프라이트 (현재는 단색)
- Pretendard 폰트 적용 (Resources/Font/ 추가 후)
- `BrushingScreenController` (타이머·구역 매핑·트래커 좌표 갱신 로직)
- 다크 테마 분기 (`ThemeManager`와 연동)
