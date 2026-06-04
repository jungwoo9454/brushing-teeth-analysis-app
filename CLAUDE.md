# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"세살버릇" (Sesalbeoreut) — a children's tooth-brushing coaching app. A camera watches the
child brush, an on-device ML pipeline classifies which mouth zone is being brushed, and the
app gives AR feedback (virtual bacteria disappearing). Built in Unity 6 with the 2D URP.

## Environment

- **Unity 6000.4.9f1** — open the project in this exact editor version (`ProjectSettings/ProjectVersion.txt`).
- There are no command-line build/lint/test scripts. Builds, play mode, and the Unity Test
  Runner (`com.unity.test-framework`) are driven from the Unity Editor.
- Editor automation (creating/editing GameObjects, scripts, scenes, running tests) goes
  through the **MCP for Unity** integration (`com.coplaydev.unity-mcp`). Use the
  `unity-mcp-skill` skill for those tasks.
- The entry scene is `Assets/Scenes/Main.unity`.

## Submodule & Git LFS

- `Assets/Model/` is a **git submodule** (the AI team's repo,
  `brushing-teeth-analysis-model`). After cloning, run `git submodule update --init`.
  It holds the trained ONNX models and the integration spec — do not edit it from this repo.
- ONNX models and other binaries are tracked with **Git LFS** (`.gitattributes`); `git lfs
  install` must have run for the model files to be real instead of pointer stubs.

## ML inference pipeline

The full integration spec is `Assets/Model/HANDOFF_TO_APP.md` — **read it before touching
any ML code.** Per-frame flow:

1. **MediaPipe Face Mesh** (`Packages/com.github.homuler.mediapipe`) → mouth polygon,
   center, width/height, pitch.
2. **YOLOv8n-pose v3** ONNX (toothbrush detection, conf ≥ 0.4) → bbox + 2 keypoints.
3. Build a **14-dimensional feature vector** (normalized; see HANDOFF §5).
4. **MLP zone classifier** ONNX (14 → 8 classes) → zone string.
5. **Stability buffer** (window 3, min count 2) before accepting a zone.
6. `ZoneClassifier` engagement check (`in_polygon` + `mouth_overlap ≥ 0.10`) — else `None`.
7. Map 8 ML zones → 5 UI zones; trigger AR feedback.

ONNX models run via **Unity AI Inference / Sentis** (`com.unity.ai.inference`). Run YOLO on
`BackendType.GPUCompute`; run the tiny MLP on `BackendType.CPU`.

### Critical gotcha — keypoint swap

The YOLO model was trained with swapped keypoint labels. Inference code **must** read:
`head_center = keypoints[1]`, `handle_end = keypoints[0]`. Skipping the swap drops accuracy
from ~41% to ~11%. The Python files under `Assets/Model/reference_code/` are logic
references only — they are not compiled and cannot be used directly.

## Code structure

App C# code lives in `Assets/Script/` (everything under `Assets/OtherAssets/` is
third-party, e.g. LeanTween, and should not be modified):

- **`Framework/`** — reusable infrastructure.
  - `Singleton<T>` / `NoDesSingleton<T>`: base classes for managers. Both survive scene
    loads via `DontDestroyOnLoad` and auto-create the GameObject if missing. Access through
    `.ins` (not `.Instance`). Subclasses declare a `protected` constructor to block
    non-singleton construction. `NoDesSingleton` additionally guards against use after
    `OnApplicationQuit`.
  - `SceneLoader`, `AnimationEventListener` (Animator events → `UnityEvent` array),
    `YieldInstruction` (pre-allocated yield objects to avoid per-frame GC).
- **`Mng/`** — singleton managers (`FileMng`, `SoundMng`, ...). New managers follow the
  pattern `class XMng : Singleton<XMng>` with a `protected XMng() {}` constructor.

`FileMng.LoadFile` and `SoundMng` load assets from `Assets/Resources/` (e.g.
`Resources/Sound/Effect/`, `Resources/Sound/Background/`) — paths are relative to a
`Resources` folder. Source comments are written in Korean; match that convention.
