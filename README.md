# 세살버릇 (Brushing Teeth Analysis App)

AI로 아이의 양치 동작을 실시간 분석하고, AR·게임 요소로 올바른 양치 습관을 길러 주는
안드로이드 앱입니다.

## 프로젝트 개요

영유아 및 초등 저학년 아동은 양치질을 싫어하거나 올바른 방법을 몰라 충치 발생률이
높습니다. 기존의 타이머나 영상 가이드는 **실제로 잘 닦았는지 확인할 수 없다**는 한계가
있습니다. 세살버릇은 카메라로 양치 동작을 분석해 어느 구역을 닦고 있는지 판별하고,
세균이 사라지는 AR 효과와 보상 시스템으로 아이의 양치를 유도합니다.

- **주요 타겟**: 만 4~9세 영유아 및 초등 저학년 아동
- **사용 주체**: 학부모, 치과, 공공기관(교육용)
- **플랫폼**: Android

## 핵심 기능

1. **실시간 구강 구역 트래킹** — MediaPipe Face Mesh로 입술 좌표를 추적하고, 입 내부를
   5개 구역(앞니, 오른쪽 윗니, 오른쪽 아랫니, 왼쪽 윗니, 왼쪽 아랫니)으로 분할합니다.
2. **칫솔 객체 탐지** — YOLOv8n-pose로 칫솔과 칫솔모 끝 좌표를 인식하고, 구강 구역과의
   겹침을 계산해 현재 닦는 구역을 판별합니다.
3. **AR 인터랙션** — 칫솔이 닿은 구역의 가상 세균이 사라지는 시각 효과를 보여 줍니다.
4. **데이터 분석 리포트** — 아동용(별점·스티커·캐릭터 성장 등 시각적 성취 중심)과
   부모용(종합 점수·구역별 분석·AI 기반 개선 가이드·위험 알림)으로 분리 제공합니다.

## 기술 스택

| 구분 | 사용 기술 |
|---|---|
| 엔진 | Unity 6 (6000.4.9f1), 2D URP |
| 언어 | C# |
| 얼굴 인식 | MediaPipe Unity Plugin (Face Mesh) |
| 칫솔 검출 | YOLOv8n-pose v3 (ONNX) |
| 구역 분류 | MLP zone classifier (ONNX) |
| 온디바이스 추론 | Unity AI Inference (Sentis) |
| 협업 | Git/GitHub, Notion, 카카오톡/디스코드 |

## AI 추론 파이프라인

프레임마다 다음 순서로 처리합니다.

```
카메라 프레임
  → MediaPipe Face Mesh (입 폴리곤·중심·pitch)
  → YOLOv8n-pose v3 (칫솔 bbox + 키포인트 2개)
  → 14차원 특징 벡터 생성
  → MLP 분류기 (8개 zone)
  → 다중 프레임 안정화 (window 3, min count 2)
  → 입 접촉 여부(engagement) 체크
  → 5개 구역 매핑 → AR 피드백
```

> 모델은 8개 zone을 출력하지만, 앱에서는 5개 구역(앞니, 오른쪽 윗니, 오른쪽 아랫니,
> 왼쪽 윗니, 왼쪽 아랫니)으로 매핑해 사용합니다.

> ⚠️ YOLO 모델은 키포인트 라벨이 swap된 상태로 학습되었습니다. 추론 시
> `head = keypoints[1]`, `handle = keypoints[0]`로 읽어야 하며, 적용하지 않으면 정확도가
> 크게 떨어집니다. 자세한 통합 명세는 `Assets/Model/HANDOFF_TO_APP.md`를 참고하세요.

## 프로젝트 구조

```
Assets/
├── Scenes/Main.unity        # 메인 씬
├── Script/
│   ├── Framework/           # Singleton, SceneLoader 등 공용 인프라
│   └── Mng/                 # FileMng, SoundMng 등 매니저(싱글톤)
├── Model/                   # AI 모델 서브모듈 (ONNX 모델 + 핸드오프 문서)
├── Resources/               # 런타임 로드 리소스 (Sound 등)
├── Document/                # 기획서
├── Settings/                # URP 설정
└── OtherAssets/             # 서드파티 에셋 (LeanTween 등)
Packages/                    # MediaPipe, Unity MCP 등 패키지
```

- `Assets/Script/Framework`, `Assets/Script/Mng`이 직접 작성한 앱 코드입니다.
  매니저는 `Singleton<T>` / `NoDesSingleton<T>`를 상속해 `.ins`로 접근합니다.
- 개발 가이드는 저장소 루트의 [`CLAUDE.md`](CLAUDE.md)를 참고하세요.

## 시작하기

이 저장소는 서브모듈과 Git LFS를 사용합니다.

```bash
git clone --recurse-submodules <repo-url>
# 이미 클론한 경우
git submodule update --init
git lfs install && git lfs pull
```

1. **Unity 6000.4.9f1**로 프로젝트를 엽니다.
2. `Assets/Scenes/Main.unity` 씬을 실행합니다.

## 팀원

| 이름 | 전공 | 역할 | GitHub |
|---|---|---|---|
| 김정우 | 컴퓨터공학전공 | 메인 개발 | [@jungwoo9454](https://github.com/jungwoo9454) |
| 김지원 | 컴퓨터공학전공 | 서브 | [@wldnjs2520](https://github.com/wldnjs2520) |
| 김태현 | SW안전보안전공 | 서브 | [@taehyeon0128](https://github.com/taehyeon0128) |
| 이종완 | 컴퓨터공학전공 | 서브 | [@lee2003121](https://github.com/lee2003121) |
| 이준영 | 컴퓨터공학전공 | 서브 | [@wnsdud030415](https://github.com/wnsdud030415) |
| 정재영 | SW안전보안전공 | 서브 | [@qwertyuiopasdfghjcn](https://github.com/qwertyuiopasdfghjcn) |

> 서브 인원의 세부 개발 파트는 추후 업데이트 예정입니다.

## 문서

- 기획서: `Assets/Document/세살버릇_앱_기획서(초안).pdf`
- AI 통합 가이드: `Assets/Model/HANDOFF_TO_APP.md`

## 양치 점수 산출 알고리즘

양치 세션이 끝나면 `BrushingScorer`(`Assets/Script/Brushing/BrushingScore.cs`)가 결과를
**0~100점**으로 환산한다. 점수는 아래 세 평가 축을 0~1로 구한 뒤 가중 합산해 계산한다.

| 평가 축 | 가중치 | 설명 |
| --- | --- | --- |
| 구역 충족도 (coverage) | 60% | 5개 구역 각각이 목표 시간에 얼마나 도달했는지의 평균 |
| 구역 균등도 (evenness) | 20% | 가장 적게 닦은 구역 / 가장 많이 닦은 구역 |
| 지속성 (continuity) | 20% | 가장 길게 "쉰" 연속 시간이 짧을수록 높음 |

### 입력값
- `zoneSeconds[5]` — 5개 UI 구역별 누적 양치 시간(초). 인덱스 = `UiZone`
  (0=왼쪽 윗니, 1=오른쪽 윗니, 2=왼쪽 아랫니, 3=오른쪽 아랫니, 4=앞니).
  옆면(Side) 검출은 위·아래 두 구역에 동시 가산된다.
- `perZoneTarget` — 구역당 목표 시간(초). 기본 = 세션 길이 / 5 = **180 / 5 = 36초**.
- `longestIdle` — 세션 중 가장 길게 연속으로 양치를 쉰 시간(초).

### 계산식

**1) 구역 충족도 (coverage)**
```
coverage_i = clamp01(zoneSeconds[i] / perZoneTarget)   // 구역별 목표 도달률(0~1)
coverage   = average(coverage_i)                        // 5구역 평균
```
안 닦은 구역은 0이 되므로, 한쪽만 닦으면 점수가 낮아진다(균등성도 함께 반영).

**2) 구역 균등도 (evenness)**
```
evenness = (max > 0) ? min(zoneSeconds) / max(zoneSeconds) : 0
```
한 구역이라도 0이면 0, 모든 구역을 똑같이 닦으면 1.

**3) 지속성 (continuity)**
```
continuity = clamp01(1 - longestIdle / 60)
```
60초 이상 연속으로 쉬면 0점 → "3분 동안 쉬지 않고 닦았는지"를 평가한다.

**최종 점수**
```
score = round( (0.6 · coverage + 0.2 · evenness + 0.2 · continuity) × 100 )
```

### 기준점
- **100점** — 3분 내내 5개 구역을 고르게(각 36초) 닦았을 때.
- **0점** — 3분 동안 전혀 닦지 않았을 때(모든 항목 0).

### 튜닝 포인트
- 가중치(`WeightCoverage` / `WeightEvenness` / `WeightContinuity`)와 지속성 패널티 기준
  (`IdleFullPenaltySeconds = 60`)은 `BrushingScore.cs` 상단 상수에서 조정.
- 세션 길이·구역 목표 시간은 `BrushingSession`의 `autoEndAfterSeconds`(기본 180초),
  `targetTotalSeconds` / `targetPerZoneSeconds`(기본 180 / 36)로 조정.

### 결과 저장
산출된 점수는 `BrushingRecordWriter`가 `BrushingRecord`로 만들어
`Application.persistentDataPath/brushing_records.json`에 누적 저장한다(진행 시각·최종 점수·구역별 시간 포함).
저장소는 `IBrushingRecordRepository`로 추상화되어 있어 추후 DB로 교체할 수 있다.
