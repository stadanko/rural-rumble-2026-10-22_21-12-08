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

**2. Gap detection.** Each goal has "done-when" checks (`file:`, `class:`, `scene_in_build:`, `component_in_scene:`), split into *code* checks (the agent's job) and *integration* checks (scene, asmdef and Build Settings wiring, which is the Systems Architect's job). A goal is `missing`, `needs_integration`, or `done`.

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

- **Agent unit tests:** `python -m unittest discover -s Tools/GoalAgent/tests`. 15 pass. They cover scoring, gates, tie-breaks, gap states, doc parsing, write policy, and stage/apply.
- **Unity compile:** clean, with 0 errors and 0 warnings in the console.
- **Unity EditMode:** 314/315 pass, including all 3 new Start Screen tests. The one failure, `ComboTimingTests.BufferedCombo_ThirdPressOutsideWindowOfSecondPress_IsNotStrike3`, was already there and is marked `KNOWN FAILURE (reported)` in the test file. It's unrelated to this work.
- **Play Mode:** the Start scene renders (screenshot above). A simulated Space press loaded Match, and the match ran normally ([screenshot](docs/after_press_match.png)) with no console errors.
- **Gameplay unchanged:** no file under Combat, AI, Presentation, Animation, Art or Data was touched.

- **Human playtest:** completed and accepted by the designer. It covered press-to-start with an Xbox controller, title readability at monitor resolution, blink speed (600 ms), and the 0.2 s input guard.

## Run 2: Match-end handoff

**Selected because:** with the Start Screen built, `match_end_handoff` ranked first at 23. It tied with the HUD and won on lower effort; it also unblocks the Victory screen (unblocks rating 5). Run log: `runs/20261111-102522-run.json`.

**What it built:** `MatchFlow` (in Match.unity) listens for `MatchEnded`, saves the result to `GameSession.LastResult`, holds for the D-026 knockout pause (`knockoutHoldSeconds` = 1.5, adjustable in the Inspector), and then loads Victory. Until Victory exists it goes back to the Start Screen (designer decision), which gives a full Start → Match → KO → Start loop. The files are `MatchFlow.cs`, `MatchFlowRules.cs` (the pure scene decision), and `MatchFlowRulesTests.cs` (3 EditMode tests).

**Before generating:** the goal entry in `features.json` was expanded with D-026, the Victory fallback, the "subscribe in Start" timing rule, and `MatchBootstrap.cs` as context.

**Manual changes:** none to the agent's code. It compiled on the first try, and the designer approved it as-is. The only manual step was integration: adding a `MatchFlow` GameObject to Match.unity through the Editor, a purely additive scene change.

**Agent improvement found in this run:** the re-scan reported `done` *before* `MatchFlow` was placed in the scene, because no check covered scene placement. The agent now has a `component_in_scene:<scene>:<Type>` check, which matches the script's `.meta` GUID inside the scene file. All scene-based goals use it, so "done" now means "wired in", not just "written".

**Verified:** compile is clean (0 errors, 0 warnings). EditMode is 317/318; the only failure is the known `ComboTimingTests` one. In Play Mode, a real knockout (CPU at 1 HP, hit through the normal `ReceiveHit` damage path) froze the match, saved `LastResult` (Player beat CPU, 3.06 s), and loaded Start about 1.5 s later with no console errors. Human playtest: completed and accepted by the designer.

## Run 3: In-Match HUD

![HUD with damage trail](docs/match_hud.png)

**Selected because:** after the handoff, `match_hud` ranked first at 23. It has the highest impact rating (5 → +15). Run log: `runs/20261111-103700-run.json`.

**Before generating:** the goal entry in `features.json` was expanded with the designer's answers:
- Portraits show `WrestlerDefinition.Portrait` once assigned (the designer is supplying art), and fall back to the wrestler's initial until then.
- No versus banner: D-026 ties it to a control freeze, which would be a gameplay change.

**What it built:** a Clean Retro HUD (D-016) in UI Toolkit. Diamond portraits sit on the outer edges, with names above chunky bars, and both bars drain toward the center. A damage trail holds, then drains; `trailHoldSeconds` (0.4) and `trailDrainSeconds` (0.5) are adjustable in the Inspector. The files are `MatchHud.cs`, `HealthTrail.cs` (pure trail math), `MatchHud.uxml`/`.uss`, and `HealthTrailTests.cs` (10 EditMode tests).

**Manual changes:**

| # | Change | Why |
|---|---|---|
| 1 | Bar colors in `MatchHud.uss`: fill `#FFC440` (was blue/red), trail `#E6DFCC` (was `#FFFFFF`), empty `#461818` (was `#17243A`) | **Spec gap.** The approved look is an *image* (`Staging/Art/arena_mockups_round2/hud_styles.png`, row 6). The agent's prompt is text-only, so it guessed. Colors were sampled from the mockup. |
| 2 | Names uppercased (`ToUpperInvariant`) in `MatchHud.cs` | Same mockup gap: it shows "BARRY", while the data says "Barry". |
| 3 | Added a `MatchHud` GameObject (UIDocument with the shared PanelSettings and `MatchHud.uxml`, plus `MatchHud`) to Match.unity | Integration. Additive only; references assigned through `SerializedObject`, the lesson from Run 1. |

**Lesson:** visual specs need to be written into the goal as text (hex colors, casing), or the agent needs image input. A future version could pull the hex values from the mockup into `features.json` automatically.

**Verification:**
- Compile is clean. EditMode is 327/328, including all 10 `HealthTrailTests`; the only failure is the known `ComboTimingTests` one.
- The new `component_in_scene` check correctly held the goal at `needs_integration` until the HUD was placed in Match.unity.
- In Play Mode, 5 real strikes moved the CPU's fill from 462 px to 323 px (70%), anchored to the outer edge. The trail held at 462 px, then drained to 323 px within 1.5 s. Names showed as PEAUX/BARRY, initials as P/B, with no console errors.
- **Testing note:** the first screenshots showed the Start screen over the match because Start.unity was also open in the Editor's Hierarchy. That's an Editor-only situation (a build loads one scene at a time). It was fixed by closing the extra scene; no code was involved.

**Human playtest:** completed and accepted by the designer. It covered the HUD at full screen: bar thickness, name readability, trail timing, and portrait framing.

**Portraits (added after the HUD run):** the HUD shows each wrestler's in-game standing sprite (`*_stand_front.png`), cropped to the head and upper torso. Both are centered on the face with the eyes at 42% height. Crop squares are 106 px (Barry) and 97 px (Peaux), set by the designer as 20% and 10% wider than the original 88 px framing, and enlarged with nearest-neighbor scaling to 256 px so the pixels stay sharp, and a diamond-shaped transparency mask baked in. The files are `Assets/Art/UI/Portraits/{barry,peaux}_hud_portrait.png`, assigned to `Barry.asset` / `Peaux.asset`. The registry records them as derived from the approved sprite rows.

How we got there:
1. The designer first supplied AI-generated trading-card portraits. They were staged in `Staging/Art/UI/Portraits/` per D-009, registered, cropped, and re-cropped for uniform framing after feedback.
2. The designer then preferred the in-game sprites. The card crops were deleted from `Assets/` and their registry rows marked `rejected`; the source cards stay in Staging.
3. **Why the mask is baked in:** the agent's rotated `overflow: hidden` diamond didn't clip its counter-rotated child in this UI Toolkit setup, so a portrait rendered as a square over the frame. Baking the diamond into the image fixed it with no code change.

None of these art swaps touched code: the HUD reads `WrestlerDefinition.Portrait`, exactly as the goal specified.

## Run 4: Match timer (designer override)

**Selected because:** the designer asked for it directly. The ranking had Character Select first (19 vs 13), so the agent gained a `run --goal <id>` option. The designer can choose the goal, while gates and the write policy still apply, and the run log records it as an override (`runs/20261111-105224-run.json`). The HUD built in Run 3 had unblocked this goal (it depends on `match_hud`).

**Before generating:** applying the Run 3 lesson, the goal entry spelled out the visual spec as text, with values sampled from the mockup: `#E6DFCC`, 72 px, bold, a 3 px `#10151A` outline, a 4 px shadow, no box, top center. It also spelled out the D-026 rules (count up from 0:00, m:ss, no limit, stops at KO) and said to use its own UIDocument, because the agent may not edit `MatchHud.uxml`.

**What it built:** `MatchTimerView.cs` reads `CombatMatch.ElapsedSeconds` and rewrites the label only when the second changes. `MatchTimerFormat.cs` is the pure m:ss formatter. `MatchTimer.uxml`/`.uss` are its own layout, and `MatchTimerFormatTests.cs` has 11 cases.

**Manual changes:** none to the agent's code. With an explicit text spec, the agent matched the mockup on the first try, which confirms the Run 3 lesson. The only manual step was integration: a `MatchTimer` GameObject in Match.unity, added through the Editor (additive only).

**Verified:** compile is clean. EditMode is 338/339, including all 11 timer tests; the only failure is the known `ComboTimingTests` one. In Play Mode the label read `0:01` at 1.39 s and `0:04` at 4.27 s, and `0:05` with the match over after a forced KO, followed by the normal handoff to Start. No console errors. Human playtest: completed and accepted by the designer, together with the HUD.

## Safety rails (write policy)

The agent can only create **new** files under `Assets/Scripts/UI/`, `Assets/UI/`, `Assets/Scripts/Juice/`, and `Assets/Tests/{EditMode,PlayMode}/UI/`. Forbidden patterns always win: Combat, AI, Presentation, Core, Match, Animation, Art, Data, ProjectSettings, Packages, and any `.unity`, `.prefab`, `.controller`, `.anim`, `.asmdef`, `.meta` or `.inputactions` file. Policy is checked when files are staged and checked again when they are applied. Every file needs designer approval before it reaches `Assets/`.

## Running it

```
python Tools/GoalAgent/goal_agent.py plan                       # read docs, scan, gaps, ranking; no writes
python Tools/GoalAgent/goal_agent.py run                        # full loop with y/n/view approval per file
python Tools/GoalAgent/goal_agent.py run --stage-only           # generate + stage only
python Tools/GoalAgent/goal_agent.py run --goal match_timer     # designer override of the ranking
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
