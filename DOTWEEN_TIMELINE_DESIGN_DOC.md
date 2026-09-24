# DoTween Timeline Editor — Architecture & Design Document

> **Document Purpose**: Single source of truth for the DoTween Timeline Editor architecture, design decisions, roadmap, and future AI prompts to retain complete context across development cycles.

---

## 1. Original User Prompt & Core Vision

```text
"I am making my own DoTween Timeline-based editor. I already have the bare bone done using AI in Assets/DoTween Timeline Editor. But I want to rebuild it from scratch along with new additions and stuff so that I can also learn in the process. The attached image is the high level system design I came up with. I am thinking of publishing it in Unity Asset Store as soon as I can as well. 
System Flow: Once the user attaches the DotweenTimeline Component - component will have button to open timeline editor. The user can add tween blocks either from the component or the timeline. The blocks will contain information on the initial and final states of whatever we are animating. Each block will have an animation type like Fade, Scale, Move, Rotate, Fill inside which there will be sub-categories for each like ImageFade - in as well as out, CanvasGroup Fade in and out, or whatever supports fade in dotween, Scale - LocalScale and what all scale are there in dotween, Move - AnchoredPos, DoMove, etc, Rotate - AnchoredRotate, Normal Rotate, etc, Fill - DoFill for slider as well as image. Each of these animation block will have a reference to whatever it is supposed to animate and will give a warning if for the selected animation, the said gameobject doesn't have that component or the component is not setup properly (like image type to be filled instead of normal or something)."
```

---

## 2. High-Level System Architecture

### Core Diagram & Flow

```
                      +------------------------------------+
                      |           EditorWindow             |
                      |        (DoTweenTimelineWindow)     |
                      +-----------------+------------------+
                                        |
                   Implements / Controls| Uses
                                        v
+------------------------+    +----------------------------+
|    DoTweenTimeline     |<-->|         ITimeline          |
|    (MonoBehaviour)     |    | (Start, Pause, Resume,     |
+-----------+------------+    |  Stop, SkipTo, Bookmarks)  |
            |                 +----------------------------+
            v
+------------------------+
|    Tween Container     |
|   (Tracks & Blocks)    |
|  - MoveBlock           |
|  - FadeBlock           |
|  - ScaleBlock          |
|  - RotateBlock         |
|  - FillBlock           |
+------------------------+
```

### Key Components

1. **`DoTweenTimeline` (`MonoBehaviour`)**:
   - The runtime container attached to GameObjects in the scene or prefabs.
   - Holds serialized timeline settings: duration, loops, update mode (`Normal`, `UnscaledTime`), auto-play conditions (`OnEnable`, `Start`, or Manual).
   - Holds serialized blocks (or track containers).
   - Generates and runs the runtime `DG.Tweening.Sequence`.
   - Has an Inspector button: **"Open in Timeline Editor"**.

2. **`TimelineWindow` (`EditorWindow`)**:
   - Visual dockable editor window for scrubbing, arranging, stretching, and inspecting tween blocks.
   - Automatically inspects the currently selected `DoTweenTimeline` component (with lock support).
   - Provides time ruler, playhead, zooming, panning, and track view.

3. **`ITimeline` / `ITimelineController` Interface**:
   - Standardizes playback commands across both Runtime and Editor:
     - `Play()` / `Pause()` / `Resume()` / `Stop()`
     - `GotoTime(float time, bool evaluateTweens)`
     - `CurrentTime`, `Duration`, `IsPlaying`
     - `AddBookmark(float time, string name)` / `SkipToBookmark(string name)`

4. **Tween Container & Blocks (`[SerializeReference]`)**:
   - Polymorphic list of tween actions inheriting from a base `TweenBlock`.
   - Each block knows its own `startTime`, `duration`, `ease`, target reference, and parameters.
   - Capable of generating a concrete `DG.Tweening.Tween` configured at a specific timestamp.

---

## 3. Tween Blocks & Animation Types

Each block category contains specialized sub-types tailored for Unity components:

| Category | Sub-Types | Supported Target Components | Required Validations |
| :--- | :--- | :--- | :--- |
| **Fade** | Fade In, Fade Out, Value Fade | `CanvasGroup`, `Image`, `RawImage`, `Text`, `TextMeshProUGUI`, `SpriteRenderer` | Validates target has at least one graphic or CanvasGroup component. |
| **Move** | Anchored Position, World Position, Local Position | `RectTransform`, `Transform` | Warns if using Anchored Position on non-RectTransform. |
| **Scale** | Uniform Scale, Vector3 Scale, Punch/Shake | `Transform`, `RectTransform` | Validates scale != 0 if dividing or shrinking. |
| **Rotate** | Euler Angles, Local Rotation, 2D Z-Rotation, Punch | `Transform`, `RectTransform` | Validates rotation angles and shortest path options. |
| **Fill** | Fill In, Fill Out, Value Fill | `Image`, `Slider` | **Crucial:** Validates `Image.type == Image.Type.Filled` (warns if Simple/Sliced). |
| **Audio / Event** *(Future)* | Volume Fade, Pitch, Trigger Event / Marker | `AudioSource`, `UnityEvent` | Validates audio source attached. |

---

## 4. Critical Technical Architecture Solutions

### A. Non-Destructive In-Editor Scrubbing & State Snapshots
* **The Problem:** Scrubbing the timeline in edit mode modifies GameObject positions, rotations, colors, etc. If the user stops scrubbing or closes the window, GameObjects remain displaced and dirty the scene.
* **The Solution:**
  1. **Snapshot on Scrub Start**: Capture initial state of all targets (position, rotation, scale, alpha, fill) before previewing.
  2. **DOTween Manual Evaluation**: Scrub using `sequence.Goto(scrubTime, andPlay: false)`.
  3. **Restore on Scrub Stop**: When stopping preview, exiting the window, or switching selection, revert all objects to their snapshot state without triggering unnecessary scene dirtying.

### B. Tween Modes: `To`, `FromTo`, and `Relative`
Rather than only fixed initial-and-final states:
* **To (Default)**: Tweens from target's *current value at runtime* to the specified final value (prevents visual snapping when triggers overlap).
* **FromTo**: Forces target to start at `ValueA` at block start, then tweens to `ValueB`.
* **Relative**: Offsets target by `+delta` relative to its starting value (great for popups, shakes, and button bounces).

### C. Smart Validation & One-Click "Fix It" Actions
Every block implements a validation check:
* If a component is missing or misconfigured, display a warning box in both the Inspector and Timeline Window.
* Provide a **Fix It** button:
  * E.g., *"Target lacks CanvasGroup. [Click to Add CanvasGroup]"* -> calls `Undo.AddComponent<CanvasGroup>(target)`.
  * E.g., *"Image is not set to Filled. [Click to Set Image to Filled]"* -> changes `image.type = Image.Type.Filled`.

### D. Assembly Definitions & Asset Store Readiness
* **`Runtime` Assembly (`Hitwicket.DoTweenTimeline.Runtime`)**:
  * Contains `DoTweenTimeline`, `TweenBlock` base & concrete implementations, sequence builder.
  * **Strictly no `UnityEditor` references** — clean builds for Android, iOS, WebGL, Standalone.
* **`Editor` Assembly (`Hitwicket.DoTweenTimeline.Editor`)**:
  * Contains `TimelineWindow`, Custom Inspectors, Block Drawers, Preview & Snapshot Controllers.
* **Namespace Isolation**: Clean namespace structure (e.g., `DoTweenTimeline.Runtime` and `DoTweenTimeline.Editor`).

---

## 5. Phased Roadmap (MVP to Unity Asset Store)

```
Phase 1: Core Data & Factory
  ├── Base TweenBlock class (startTime, duration, ease, target)
  ├── Concrete Blocks: MoveBlock, FadeBlock, ScaleBlock, RotateBlock, FillBlock
  └── Sequence compilation engine (DoTweenTimeline.GenerateSequence)

Phase 2: In-Editor Preview & Snapshot Engine
  ├── TargetStateSnapshot (captures & restores transform/graphic states)
  └── Non-destructive timeline scrubbing via sequence.Goto()

Phase 3: Visual Timeline Window
  ├── Header controls: Play, Pause, Stop, Scrub bar, Time Ruler, Zoom/Pan
  ├── Track / Block Visuals: Draggable blocks, resize handles for duration
  └── Selection sync: Selecting a block highlights its target in the hierarchy

Phase 4: Smart Validation & UX Polish
  ├── Warnings for missing/misconfigured components
  ├── One-click auto-fix buttons
  ├── Undo/Redo integration for all block edits

Phase 5: Packaging & Store Release
  ├── Asmdefs, Demo Scene with common UI animations
  ├── Documentation manual, clean code comments, zero compiler warnings
```

---

## 6. Prompt to Restore Context in Future Sessions

Copy and paste the block below into any future AI chat to immediately restore full context:

```text
We are building a Unity Asset Store product: "DoTween Timeline Editor", a custom visual timeline editor for DOTween sequences.
Design Doc reference: DOTWEEN_TIMELINE_DESIGN_DOC.md.
Key Architecture:
- DoTweenTimeline (MonoBehaviour runtime container with [SerializeReference] blocks)
- TimelineWindow (EditorWindow with scrubbing, ruler, and block tracks)
- ITimeline interface for playback controls (Start, Pause, Resume, Stop, Goto, Bookmarks)
- Categories: Fade, Scale, Move, Rotate, Fill with specific sub-types and component validations (with one-click fix buttons)
- State snapshotting for non-destructive in-editor timeline scrubbing
- Clean split between Runtime and Editor assembly definitions.
Please maintain this architectural vision and adhere to Unity Editor tooling best practices.
```
