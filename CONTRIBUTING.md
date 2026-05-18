# 협업 가이드 (CONTRIBUTING)

"세살버릇" 프로젝트의 Git 협업 규칙입니다. 기여 전 반드시 한 번 읽어 주세요.

## 브랜치 전략 — 2-tier GitHub Flow

이 프로젝트는 **`main` + 기능 브랜치** 2단계 구조를 사용합니다. `develop`·`release` 같은 상시 브랜치는 두지 않습니다.

```
main  ─────●────────●────────●───────  (항상 빌드/플레이 가능한 안정 브랜치)
            \        \        \
             feature/  fix/    docs/
```

- **`main`**: 항상 Unity에서 빌드·플레이가 되는 안정 상태를 유지합니다. **직접 푸시 금지** — 모든 변경은 PR로만 들어옵니다.
- **기능 브랜치**: 작업 단위마다 `main`에서 새로 따고, 작업이 끝나면 PR → 머지 후 삭제합니다. 짧게(1~3일) 살리고 자주 머지하세요.

### 브랜치 이름 규칙

`<타입>/<짧은-설명>` 형식. 설명은 영문 소문자 + 하이픈을 권장합니다.

| 타입 | 용도 | 예시 |
|---|---|---|
| `feature/` | 새 기능 | `feature/zone-ui`, `feature/onnx-inference` |
| `fix/` | 버그 수정 | `fix/keypoint-swap` |
| `docs/` | 문서 작업 | `docs/readme-update` |
| `chore/` | 설정·빌드·잡일 | `chore/gitignore` |

## 작업 흐름

```bash
# 1. 최신 main 받기
git checkout main
git pull origin main

# 2. 기능 브랜치 생성
git checkout -b feature/zone-ui

# 3. 작업 → 커밋 → 푸시
git add .
git commit -m "구역 UI 레이아웃 추가"
git push -u origin feature/zone-ui

# 4. GitHub에서 main으로 PR 생성 → 리뷰 → 머지
# 5. 머지 후 브랜치 삭제
git checkout main
git pull origin main
git branch -d feature/zone-ui
```

## 커밋 메시지

- 한국어로 작성하고, **무엇을 했는지**가 드러나게 씁니다. (프로젝트 주석도 한국어 컨벤션)
- 한 커밋은 한 가지 일만. 예) `구역 UI 레이아웃 추가`, `키포인트 스왑 버그 수정`

## PR 규칙

- PR 제목·설명에 작업 내용과 테스트 방법을 적습니다.
- **주 개발자 1인 리뷰 승인 후 머지**합니다.
- `main` 브랜치 보호 규칙(직접 푸시 차단, PR 필수)을 GitHub Settings에서 설정해 두는 것을 권장합니다.

## Unity 작업 시 주의 — 가장 중요 ⚠️

`.unity` 씬과 `.prefab`은 YAML이라 머지 충돌이 매우 어렵습니다. 충돌은 브랜치 수가 아니라 **동시 편집**에서 발생합니다.

- **`Assets/Scenes/Main.unity`는 한 번에 한 명만 편집합니다.** 씬을 건드리기 전 팀 메신저에 "씬 작업 중"을 공유하세요.
- 보조 인원은 가능하면 씬을 직접 수정하지 않도록 작업을 분배합니다.
- `ProjectSettings` → Editor → **Asset Serialization = Force Text** 설정을 유지합니다(기본값).
- 작업 시작 전 항상 `git pull`로 최신 `main`을 받아 충돌 범위를 줄입니다.

## Git LFS

ONNX 모델 등 바이너리는 Git LFS로 관리됩니다. 클론 직후 반드시 실행하세요.

```bash
git lfs install
git lfs pull
```

## 서브모듈 — `Assets/Model/`

`Assets/Model/`은 AI팀 레포(`brushing-teeth-analysis-model`)의 **서브모듈**입니다.

- 클론 직후: `git submodule update --init`
- **이 레포에서 서브모듈 내부를 편집하지 않습니다.**
- 모델 버전 갱신(서브모듈 포인터 변경)은 다른 변경과 섞지 말고 **별도 PR**로 올립니다.
