# Goal-Oriented Coding Agent (Assignment #5)

The agent reads Rural Rumble's design docs, scans the Unity project, finds the required features that aren't built yet, decides which one to build first, writes the code with Claude, and re-scans until that goal is closed.

![Start Screen built by the agent](docs/start_screen.png)

## What the agent built: the Start Screen

The game's first screen: "RURAL RUMBLE" in palette yellow over the trailer-park arena backdrop, with a blinking **Press Any Button to Start** prompt. Any keyboard or gamepad button continues to Character Select once that scene exists. Until then it goes straight to the Match, so the game is playable from the first scene today (Build Settings index 0).

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/StartScreenFlow.cs` | Pure decision: Character Select if it's in Build Settings, otherwise Match |
| `Assets/Scripts/UI/StartScreenController.cs` | Thin MonoBehaviour: prompt blink, any-button press, scene load |
| `Assets/UI/StartScreen.uxml` / `.uss` | UI Toolkit layout and style (Style Guide palette, arena backdrop) |
| `Assets/Tests/EditMode/UI/StartScreenFlowTests.cs` | 3 EditMode tests for the scene decision |

The agent's raw output is preserved in `staging/start_screen/` (with its `manifest.json` of integration notes and assumptions), so you can diff it against what was accepted.

## Why the agent selected it

**1. Goals come from the docs.** `features.json` lists 11 candidate goals. Each cites the design-doc section that requires it, and the agent confirms that citation by parsing the doc. For example, the Start Screen is backed by GDD › MVP Scope: *"Game Flow: Start screen, character selection, match, and victory/defeat presentation."*

**2. Gap detection.** Each goal has "done-when" checks (`file:`, `class:`, `scene_in_build:`), split into *code* checks (the agent's job) and *integration* checks (scene, asmdef and Build Settings wiring, which is the Systems Architect's job). A goal is `missing`, `needs_integration`, or `done`.

**3. Gates, then a weighted sum.** `priority_score()` returns 0 for any goal that fails a hard gate. Otherwise it scores `impact×3 + unblocks×2 + testability×2 + effort×(−1)` from designer ratings (0–5). The weights are in `config.json`; the ratings are in `features.json`.

| Gate | Example |
|---|---|
| Dependency not built | Character Select needs Start Screen; Victory needs the match-end handoff; Match timer needs the HUD |
| Required assets missing | Sound effects: `Assets/Audio/` is empty |
| Unsafe | Per-frame animation speed would modify the protected animation builder |

The ranking on the first run (`runs/20261111-093600-run.json`):

| # | Goal | Score | Why |
|---|---|---|---|
| **1** | **start_screen** | **24** | impact 9, unblocks 8, testability 8, effort −1 |
| 2 | match_end_handoff | 23 | tie with HUD; lower effort wins the tie-break |
| 3 | match_hud | 23 | highest impact (15) but effort −2 and few dependents |
| 4–6 | screen shake, tuning overlay, pause menu | 11, 10, 10 | low unblocks |
| — | character_select, victory_screen, match_timer, sound_effects | 0 | blocked |
| — | anim_speed_tuning | 0 | unsafe |

The Start Screen won by one point because it unblocks the rest of the screen flow and is small and easy to test. The designer chose these weights during planning, over an impact-heavy option that would have picked the HUD.

**4. Write, then re-scan.** After the files were applied, the re-scan moved `start_screen` from `missing` to `needs_integration` and unblocked Character Select (0 → 19). After integration it reads `done`, and the next run would pick `match_end_handoff`.

## Manual changes before accepting the code

| # | Change | Why |
|---|---|---|
| 1 | Added `using UnityEngine.InputSystem.Utilities;` to `StartScreenController.cs` | **Compile error.** `onAnyButtonPress.Call()` is an extension method in that namespace. Partly the prompt's fault: it recommended `.Call()` without naming the namespace. `prompts/codegen.md` now says so. |
| 2 | Input delay measured from `OnEnable` (`_enabledAt`) rather than app start | **Logic bug** found in review: `Time.unscaledTime < 0.2` only works the first time the game boots, so it would fail when returning to the title later. |
| 3 | Created `Assets/Scripts/UI/RuralRumble.UI.asmdef` (Core, Combat, Match, InputSystem, per ARCHITECTURE §2) | The agent may not write `.asmdef` files; that's the Architect's job. |
| 4 | Added `RuralRumble.UI` to the EditMode test asmdef | So the agent's tests compile. |
| 5 | Created `Assets/UI/RuralRumbleRuntimeTheme.tss` and `RuralRumblePanelSettings.asset` (scale with screen size, 1920×1080) | UI Toolkit needs a PanelSettings and a theme to render. |
| 6 | Created `Assets/Scenes/Start.unity` (camera plus `StartScreen` with UIDocument and controller) and put it at Build Settings index 0 | The agent never edits scenes or Build Settings. |

Changes 1–2 fix the agent's code (`diff staging/start_screen/files/... Assets/...` shows them). Changes 3–6 are integration the write policy deliberately keeps away from the agent. The first scene build also saved an empty PanelSettings reference (an integration-script mistake, not agent code), which showed up as a blank screen in Play Mode and was fixed with a `SerializedObject` assignment.

## Verification

- **Agent unit tests:** `python -m unittest discover -s Tools/GoalAgent/tests`. 14 pass. They cover scoring, gates, tie-breaks, gap states, doc parsing, write policy, and stage/apply.
- **Unity compile:** clean, with 0 errors and 0 warnings in the console.
- **Unity EditMode:** 314/315 pass, including all 3 new Start Screen tests. The one failure, `ComboTimingTests.BufferedCombo_ThirdPressOutsideWindowOfSecondPress_IsNotStrike3`, was already there and is marked `KNOWN FAILURE (reported)` in the test file. It's unrelated to this work.
- **Play Mode:** the Start scene renders (screenshot above). A simulated Space press loaded Match, and the match ran normally ([screenshot](docs/after_press_match.png)) with no console errors.
- **Gameplay unchanged:** no file under Combat, AI, Presentation, Animation, Art or Data was touched.

- **Human playtest:** completed and accepted by the designer. It covered press-to-start with an Xbox controller, title readability at monitor resolution, blink speed (600 ms), and the 0.2 s input guard.

## Safety rails (write policy)

The agent can only create **new** files under `Assets/Scripts/UI/`, `Assets/UI/`, `Assets/Scripts/Juice/`, and `Assets/Tests/{EditMode,PlayMode}/UI/`. Forbidden patterns always win: Combat, AI, Presentation, Core, Match, Animation, Art, Data, ProjectSettings, Packages, and any `.unity`, `.prefab`, `.controller`, `.anim`, `.asmdef`, `.meta` or `.inputactions` file. Policy is checked when files are staged and checked again when they are applied. Every file needs designer approval before it reaches `Assets/`.

## Running it

```
python Tools/GoalAgent/goal_agent.py plan                       # read docs, scan, gaps, ranking; no writes
python Tools/GoalAgent/goal_agent.py run                        # full loop with y/n/view approval per file
python Tools/GoalAgent/goal_agent.py run --stage-only           # generate + stage only
python Tools/GoalAgent/goal_agent.py apply <goal> --approve all # or --approve a,b --reject c
```

Requirements: Python 3.10+ (standard library only), plus the Claude Code CLI on PATH and logged in. Generation reuses Assignment 4's `Tools/DynamicContent/llm.py` (`claude -p`, no API key). Each run writes a decision log to `runs/`.

| File | Role |
|---|---|
| `goal_agent.py` | Doc reader, codebase scan, gap detection, gates, `priority_score()`, code-writing step, approval, iteration loop |
| `features.json` | Goal catalog: doc citation, checks, dependencies, ratings |
| `config.json` | Weights, per-run feature cap, model, write policy |
| `prompts/codegen.md` | System prompt for the code-writing step |
| `tests/test_goal_agent.py` | Unit tests for the reasoning layer |
