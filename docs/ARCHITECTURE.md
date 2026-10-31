# Rural Rumble Architecture

Authority for folder ownership, shared interfaces, and data flow. Owned by the Systems Architect. Agents follow this document; changes to it go through the Systems Architect.

Items marked **Proposed — needs designer approval** are the Architect's recommendation where the design documents leave a choice open. Agents may build on them, but they are not decisions until recorded in DECISION_LOG.md. Open questions Q-001 to Q-004 are not resolved here.

Project baseline: Unity 6000.3, URP 2D renderer, Input System 1.20, Unity Test Framework 1.6, Windows standalone. `Assets/Scripts/AutocompleteTestScript.cs` is a user test file: leave it alone, do not build on it.

---

## 1. Folder ownership

Only the owner creates or edits files in a folder. Others request changes through the main session.

| Path | Owner |
|---|---|
| `Assets/Scripts/Core/` (interfaces, events, game session, scene loading, player input adapter) | Systems Architect |
| `Assets/Scenes/`, Build Settings scene list | Systems Architect |
| `Assets/Settings/` (incl. `InputSystem_Actions.inputactions`, URP and 2D renderer assets), `ProjectSettings/`, `Packages/` | Systems Architect |
| `Assets/Data/Wrestlers/` (wrestler definitions, roster) | Systems Architect (wires references delivered by others) |
| `Assets/Prefabs/Arena/` | Systems Architect (art from Art Wrangler, obstacle markers from Combat) |
| `Assets/Editor/Build/` | Systems Architect |
| All `.asmdef` files | Systems Architect |
| `Assets/Scripts/Combat/`, `Assets/Scripts/AI/`, `Assets/Data/Tuning/` | Combat State & AI |
| `Assets/Scripts/Presentation/`, `Assets/Animation/`, `Assets/Prefabs/Wrestlers/`, `Assets/Editor/Tooling/` | Unity Animation & Tooling |
| Import settings for `Assets/Art/Wrestlers/`, `Assets/Art/Arena/`, `Assets/Art/Effects/` | Unity Animation & Tooling |
| `Assets/Scripts/UI/`, `Assets/UI/` (layouts, styles), `Assets/Prefabs/UI/`, import settings for `Assets/Art/UI/` | UI & Match Flow |
| `Assets/Tests/EditMode/`, `Assets/Tests/PlayMode/`, `docs/ACCEPTANCE_TESTS.md` | QA & Testing |
| Adding approved art files to `Assets/Art/**`; `Staging/Art/`; `Assets/Art/_Generated/` (AI generation working folder, never referenced by the game, D-010); `Tools/ArtPipeline/` (Python cleanup scripts, outside Unity) | Art Wrangler |
| `Assets/Audio/` (files and import settings); `Staging/Audio/` | Sound Wrangler (future) |
| `Assets/Scripts/Juice/`, `Assets/Prefabs/Effects/` | Juice Agent (future) |
| `docs/ASSET_REGISTRY.csv` | Provenance Clerk (future). Until the Clerk exists, whoever adds an asset adds its row. |
| `docs/DECISION_LOG.md`, `docs/GDD.md`, `docs/COMBAT_SPEC.md`, `docs/CONTROLS.md`, `docs/STYLE_GUIDE.md` | Designer. Agents edit only with designer approval. |

Changes from the proposed table in `systems-architect.md`:
- UI sprites live in `Assets/Art/UI/` (Art Wrangler adds files, UI & Match Flow owns import settings) instead of `Assets/UI/`, so all art has one drop location and one provenance trail. `Assets/UI/` holds only layouts and styles. **Approved (D-009).** Reason: one place for sourced art keeps provenance checks simple.
- Unverified assets go in `Staging/` at the project root, outside `Assets/`, so Unity never imports them into the build. Approved assets move into `Assets/Art/` or `Assets/Audio/`. **Approved (D-009).** Reason: CLAUDE.md requires a staging area and keeping unverified assets out of production; outside `Assets/` guarantees that.
- `Assets/Data/Wrestlers/` and `Assets/Prefabs/Arena/` added (Architect).

## 2. Assembly definitions

Runtime code is split so EditMode tests reference combat rules without scenes.

| Assembly | Folder | References | Notes |
|---|---|---|---|
| `RuralRumble.Core` | `Assets/Scripts/Core/` | `Unity.InputSystem` | Shared types. No gameplay rules. |
| `RuralRumble.Combat` | `Assets/Scripts/Combat/` | `RuralRumble.Core` | Rules (plain C#) + thin MonoBehaviour runner + `CombatTuning` type. |
| `RuralRumble.AI` | `Assets/Scripts/AI/` | `RuralRumble.Core`, `RuralRumble.Combat` | CPU FSM, plain C#. |
| `RuralRumble.Presentation` | `Assets/Scripts/Presentation/` | `RuralRumble.Core`, `RuralRumble.Combat` | Animation drivers. |
| `RuralRumble.UI` | `Assets/Scripts/UI/` | `RuralRumble.Core`, `RuralRumble.Combat`, `Unity.InputSystem` | Screens, HUD, match flow. |
| `RuralRumble.Juice` (future) | `Assets/Scripts/Juice/` | `RuralRumble.Core`, `RuralRumble.Combat` | Feedback only. |
| `RuralRumble.Editor.Build` | `Assets/Editor/Build/` | runtime assemblies as needed | Editor platform only. |
| `RuralRumble.Editor.Tooling` | `Assets/Editor/Tooling/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.Presentation` | Editor platform only. |
| `RuralRumble.Tests.EditMode` | `Assets/Tests/EditMode/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.AI`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `nunit.framework.dll` | Editor platform only; define constraint `UNITY_INCLUDE_TESTS`. |
| `RuralRumble.Tests.PlayMode` | `Assets/Tests/PlayMode/` | all runtime assemblies, `UnityEngine.TestRunner`; `nunit.framework.dll` | Any platform; define constraint `UNITY_INCLUDE_TESTS`. |

Dependency direction: Core ← Combat ← (AI, Presentation, UI, Juice). Combat never references Presentation, UI, AI, or Juice. Nothing references Tests.

## 3. Scenes and flow

Screen flow (D-003): **Start → CharacterSelect → Match → Victory**. No mode select.

| Build index | Scene | Contents |
|---|---|---|
| 0 | `Assets/Scenes/Start.unity` | Start screen UI |
| 1 | `Assets/Scenes/CharacterSelect.unity` | Character select UI |
| 2 | `Assets/Scenes/Match.unity` | Camera, arena prefab, `MatchBootstrap`, HUD |
| 3 | `Assets/Scenes/Victory.unity` | Victory screen UI |

- **Loading:** `SceneLoader.Load(SceneId)` (Core) calls `SceneManager.LoadScene(name, LoadSceneMode.Single)`. Scene names live only in `SceneNames` constants. No persistent bootstrap scene, no additive loading, no Addressables. **Proposed — needs designer approval.** Reason: four small scenes need nothing more.
- UI & Match Flow calls `SceneLoader`; it never hard-codes scene names.
- `SampleScene.unity` stays untouched; `Match.unity` is created new and the Build Settings list is replaced with the four scenes above. Deleting `SampleScene` needs designer approval. **Proposed — needs designer approval.**
- What follows the Victory screen is not defined (see Gaps).

### 3.1 Rendering scale (D-013)

- **Native resolution 960×540**, integer-scaled: ×2 at 1080p, ×3 at 1440p, ×4 at 4K.
- **Pixels per unit: 60** for all game sprites (wrestlers, arena, effects), so the screen is 16×9 world units. Barry (240 px) is 4.0 units tall; Peaux (215 px) about 3.6.
- **Match camera (D-017):** a plain orthographic camera driven by a `MatchCamera` component (Systems Architect, `Assets/Scripts/Core/`). Not URP's `PixelPerfectCamera`, which can't zoom smoothly.
  - **Default view:** orthographic size 4.5 (960×540 native, pixel-perfect at ×2 on 1080p). At this zoom, snap the camera position to 1/60 unit so pixels stay on the grid.
  - **Follow:** horizontally centers on the midpoint of the two wrestlers, clamped so the view never leaves the arena. Vertically framed on the walkable floor.
  - **Zoom:** when the wrestlers' horizontal separation would put either one past the screen edge margin, orthographic size grows so both stay in view, up to the size that shows the whole arena. It eases back to the default as they close in.
  - **Inspector values** (serialized fields on `MatchCamera`, editable in Play Mode): `defaultVisibleWidthFraction` = 0.6, `maxVisibleWidthFraction` = 1.0 (whole arena), `edgeMargin` (world units before zooming starts), `zoomSmoothTime`, `followSmoothTime`. Camera framing is presentation, not combat, so these are not in the combat tuning asset (D-005).
  - Reads wrestler positions through `IWrestlerView` (4.2). Juice's camera shake stays on a child offset (4.2).
- **Arena dimensions:** the default view (960 px) is 60% of the arena width, so the arena is **1600 native px wide** (26.7 units). The walkable floor's full depth fits within the default 540-px-tall view. At full zoom-out the screen shows 1600×900, so the arena art is **1600×900 native px** (26.7×15 units): the extra height is sky and treeline above and foreground below, seen only when zoomed out.
- **Arena layout (native px, origin top-left of the 1600×900 canvas). Approved (D-018).** Art: `Assets/Art/Arena/arena_backdrop.png` (pivot top-left), `arena_truck.png` and `arena_pool.png` (pivot bottom-left, so a sprite's pivot sits at its left x and base row). 60 PPU: canvas x px ÷ 60 = world x; canvas row ÷ 60 = world y downward from the backdrop's top edge.
  - Default view vertical band: rows 300–840 (camera at default zoom is framed here).
  - Walkable feet range: rows 580 (back, in front of the trailer) to 820 (front); full width minus obstacles.
  - Layers, back to front: `backdrop` (sky, pines, trailer, floor; one image), then depth-sorted by base row (Sprite Sort Point = Pivot): truck (left x −40, base row 650, 502×300), pool (right edge, base row 640, 560×367), wrestlers. Anything with a base row below a wrestler's feet draws in front of that wrestler.
  - Obstacles: the trailer is the back wall (invisible collider along row 580). The truck and pool get colliders at their footprints; Combat decides their injury rules (STYLE_GUIDE Arena).
- **Player settings (applied):** default window 1920×1080, Fullscreen Window.
- **Sprite import (applied to `Assets/Art/Wrestlers/Barry/barry_stand_front.png`):** Point filter, uncompressed, no mipmaps, PPU 60, pivot Bottom Center (feet), Full Rect mesh. Unity Animation & Tooling applies the same to every wrestler sprite and sheet it imports.
- UI canvases scale against 1920×1080 (UI & Match Flow's call on Canvas Scaler settings).

## 4. Seams

### 4.1 Input abstraction (player and CPU)

```csharp
// Core. One per wrestler. Combat calls Read() exactly once per combat tick.
public interface IWrestlerInput { WrestlerIntent Read(); }

public readonly struct WrestlerIntent {
    Vector2 Move;               // raw stick/WASD, each axis -1..1; Combat quantizes to 8 directions
    ButtonState LockUpGrapple;  // A / Left Shift
    ButtonState RunWhip;        // B / Space
    ButtonState Strike;         // X / Right Shift
}
public readonly struct ButtonState { bool Pressed; /* went down since last Read */ bool Held; }
```

- Intents are buttons, not actions. Combat decides what a press means in the current state (A = lock up or grapple, B = run or whip, Strike/Grapple presses while Knocked Down = mash). This keeps CONTROLS.md logic in one place.
- `PlayerInputSource : MonoBehaviour, IWrestlerInput` (Core) reads the Input Actions asset. Combat never touches devices or `UnityEngine.InputSystem`.
- `OpponentBrain : IWrestlerInput` (AI, plain C#) produces the CPU's intents from read-only views (`IWrestlerView` of itself and the player) and `CombatTuning`. It evaluates on its decision interval, not every frame (GDD), and holds its last intent between evaluations. It cannot call combat methods directly.
- Tests use a `ScriptedInput : IWrestlerInput` that returns queued intents.
- Input Actions asset: the template `Player` map is replaced by a `Wrestler` map with actions `Move`, `LockUpGrapple`, `RunWhip`, `Strike`, bound per CONTROLS.md and D-002 (Shift bindings subject to Q-001). The template `UI` map is kept for menus; confirm/back bindings wait on Q-002. **Proposed — needs designer approval** (map and action names only; bindings are already decided).

### 4.2 Combat → presentation

Combat publishes; presentation, UI, and Juice subscribe. None of them change combat state, positions, health, or timing.

```csharp
// Combat, plain C#. Read-only per-wrestler view.
public interface IWrestlerView {
    WrestlerId Id;            // Player or Cpu (Core enum)
    CombatState State;        // exactly the 12 COMBAT_SPEC states
    MoveId CurrentMove;       // None, Strike1, Strike2, Strike3, LockUp, Bodyslam, IrishWhip, BreakLockUp
    MovePhase Phase;          // None, Startup, Active, Recovery
    float Health; float MaxHealth;
    Vector2 Position;         // arena plane: x = left/right, y = depth
    Vector2 Facing;           // last 8-way direction; presentation derives left/right and front/back
}

// Combat, plain C#. One instance per match; all events fire inside CombatMatch.Tick.
public sealed class CombatEvents {
    event Action<WrestlerId, CombatState /*from*/, CombatState /*to*/> StateChanged;
    event Action<WrestlerId, MoveId, MoveTiming> MoveStarted;
    event Action<WrestlerId, MoveId, MovePhase> MovePhaseChanged;
    event Action<PairedMoveInfo> PairedMoveStarted;   // lock-up, bodyslam, Irish Whip, break
    event Action<HitInfo> HitLanded;                  // attacker, defender, move, damage, contact point
    event Action<WrestlerId, Vector2> ObstacleHit;    // running/whipped wrestler hits an arena object
    event Action<WrestlerId, float> HealthChanged;    // new health
    event Action<WrestlerId, float> KnockedDown;      // knockdown duration
    event Action<MatchResult> MatchEnded;             // winner, loser, elapsed seconds
}
```

`CombatState` values: Standing, Walking, Running, Striking, Struck, LockUp, LockedUp, Grappling, Grappled, KnockedDown, Recovering, KnockedOut. Do not add states without a COMBAT_SPEC change.

**Wrestler objects.** A wrestler prefab (`Assets/Prefabs/Wrestlers/`) is presentation only: root with `WrestlerView` (Presentation), child `SpriteRenderer`, `Animator`. It holds no combat components. Combat's scene-level `CombatMatchRunner` writes each wrestler root's `transform.position` from `IWrestlerView.Position` every tick; presentation only animates children. **Proposed — needs designer approval.** Reason: one owner per prefab and per transform; no two agents edit the same prefab.

Both wrestlers share one Animator controller; each wrestler gets an `AnimatorOverrideController` with its own clips. **Proposed — needs designer approval.** Reason: one state graph matches the shared combat framework.

**Attack timing.** Combat is the authority for when a hit can land.
- Every move has a `MoveTiming { float Startup; float Active; float Recovery; }` (seconds) in `CombatTuning`. Combat checks for contact only during `Active`.
- `MoveStarted` carries the current `MoveTiming`. Presentation looks up the clip in a `MoveAnimationTable` (ScriptableObject in `Assets/Animation/`, owned by Animation & Tooling) that records each clip's contact time at native speed, and sets playback speed so the contact frame lands at `Startup`. `MovePhaseChanged` lets presentation resync.
- Animation events never trigger hits or state changes. If a clip cannot match the timing, Animation & Tooling reports it; it does not change tuning.
- An Editor validator in `Assets/Editor/Tooling/` reports moves whose clip contact time and tuned `Startup` would require extreme playback speeds.
- **Proposed — needs designer approval.** Reason: keeps hit detection testable without Animators, and Play Mode tuning edits stay in sync because speed is computed at each move start.

**Hit detection.** Plain C# box overlap on the arena plane (`ArenaRect` hurt/hit boxes with a depth tolerance), computed in `CombatMatch.Tick`, not Physics2D colliders or triggers. Box sizes and ranges are tuning values. Both wrestlers are resolved in one tick, so "first strike to land wins" is deterministic. Arena bounds and obstacles are `ArenaRect`s collected from `ArenaObstacleMarker` components (Combat) on the arena prefab at match start. **Proposed — needs designer approval.** Reason: EditMode-testable, deterministic, no physics callback ordering.

**Tick.** `CombatMatchRunner.Update` calls `CombatMatch.Tick(dt, playerIntent, cpuIntent)` with `Time.deltaTime` clamped to a maximum step. Rules never read `Time`. **Proposed — needs designer approval.** Reason: simplest loop with no lost button presses; tests pass explicit `dt`.

**Depth sorting.** The 2D renderer already sorts by Y (`Renderer2D.asset` transparency sort axis 0,1,0); wrestlers and arena props use the same sorting layer.

**Juice (future).** Subscribes to `HitLanded`, `ObstacleHit`, `KnockedDown`, `MatchEnded`. Never sets `Time.timeScale` or changes combat. Hitstop would need a pause hook in `CombatMatchRunner` that the Architect adds only with designer approval. Camera shake applies to a child offset of the camera, not the camera the Architect places.

### 4.3 Combat → UI

UI reads `IWrestlerView.Health / MaxHealth` (bars only, never numbers), subscribes to `HealthChanged` and `MatchEnded`, and reads `CombatMatch.ElapsedSeconds` for the timer. `MatchResult { WrestlerId Winner; WrestlerId Loser; float ElapsedSeconds; }`.

- The match timer counts up (STYLE_GUIDE) and stops at knockout. Combat owns it because it is match state.
- On `MatchEnded`, UI's `MatchFlow` stores the result in `GameSession.LastResult` and loads Victory. Victory builds "Victory! {Winner} knocked out {Loser} in {timer}" plus "Congratulations!" or "Game Over!" from `MatchResult` and the definitions in `GameSession.Setup`.
- `MatchBootstrap` exposes the `CombatMatch` to the HUD (`MatchBootstrap.Current`, set in `Awake`, cleared in `OnDestroy`). UI must not find wrestlers by name or tag.
- **UI system: UI Toolkit** (UXML/USS in `Assets/UI/`, one `UIDocument` per screen prefab in `Assets/Prefabs/UI/`). **Proposed — needs designer approval.** Reason: layouts are plain text that agents can write and review without editing prefab YAML. UI & Match Flow still runs the `unity:ui` skill before starting.

### 4.4 Character selection → match

```csharp
// Core
public sealed class WrestlerDefinition : ScriptableObject {
    string DisplayName; Sprite Portrait; GameObject Prefab; // Prefab = presentation prefab
}
public sealed class WrestlerRoster : ScriptableObject { WrestlerDefinition[] Wrestlers; } // exactly 2 in MVP
public readonly struct MatchSetup { WrestlerDefinition Player; WrestlerDefinition Cpu; }
public static class GameSession { MatchSetup Setup; MatchResult LastResult; bool HasSetup; }
```

- Assets: `Assets/Data/Wrestlers/Barry.asset`, `Peaux.asset`, `WrestlerRoster.asset`. Definitions hold no combat stats; both wrestlers share `CombatTuning` (D-005).
- CharacterSelect sets `GameSession.Setup` (player's pick; CPU gets the other) and loads Match.
- `MatchBootstrap` (Core, in Match scene) reads `GameSession.Setup`, instantiates both prefabs, creates `PlayerInputSource` and `OpponentBrain`, builds `CombatMatch`, and hands it to `CombatMatchRunner`. If `HasSetup` is false (Match opened directly in the Editor), it uses roster entry 0 as player and 1 as CPU.
- `GameSession` resets in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method so Play Mode without domain reload starts clean.
- **Proposed — needs designer approval.** Reason: a static holder is the simplest way to cross one scene load; no DontDestroyOnLoad objects to manage.

### 4.5 Testability

- All rules live in plain C# in `RuralRumble.Combat` and `RuralRumble.AI`: `CombatMatch`, `WrestlerStateMachine`, `ComboTracker`, `LockUpController`, `HealthModel`, `KnockdownTimer`, `ArenaGeometry`, `OpponentBrain`. Names are guidance; the split is Combat's call.
- No rule class reads `Time`, `Input`, scenes, or MonoBehaviours. Time arrives as `dt`; input as `WrestlerIntent`; tuning as a `CombatTuning` reference.
- MonoBehaviours (`CombatMatchRunner`, `ArenaObstacleMarker`) only connect rules to Unity.
- EditMode tests build a `CombatMatch` with `ScriptedInput`, a `CombatTuning` instance (`ScriptableObject.CreateInstance<CombatTuning>()` gives COMBAT_SPEC defaults, or load the project asset), and arena rects. Assertions read thresholds and timings from that instance.
- PlayMode tests cover scene flow and the runner.

## 5. Combat tuning asset

- Type: `CombatTuning : ScriptableObject` in `Assets/Scripts/Combat/Tuning/`. Asset: `Assets/Data/Tuning/CombatTuning.asset`. One asset shared by both wrestlers and the CPU.
- Rules hold a reference to the asset and read fields at the moment of use. Never copy values into fields in constructors, `Awake`, or `Start`. Inspector edits in Play Mode apply on the next use.
- Edits to a ScriptableObject in Play Mode persist after exiting Play Mode. Agents must not reset or overwrite tuned values in the asset; field initializers carry the COMBAT_SPEC defaults and apply only to new instances.
- Fields, grouped as in COMBAT_SPEC Tuning Values:
  - Timing: combo window (0.5 s), lock-up timer (2.0 s).
  - Health and Damage: max health (100), Recovering threshold (30), Strike 1/2/3 damage (3/4/6), bodyslam damage (12), Irish Whip impact damage (8), obstacle collision damage (8).
  - Recovery and Knockdown: recovery rate (1/s), recovery cap (30), minimum knockdown time (1.0 s), maximum knockdown time (3.0 s), mash reduction per press (8%), shortest possible knockdown (0.5 s).
- Values not in COMBAT_SPEC that Combat adds as starting points, each reported for designer review: `MoveTiming` per move, walk/run speeds, Recovering walk-speed multiplier, hit/hurt box sizes, lock-up range, paired-move positioning offsets, Struck duration, CPU decision interval, ranges, and action probabilities (GDD: "configurable probabilities").
- Rules (what is allowed, what happens) are never added to the asset as settings to avoid a design decision.

## 6. Two-wrestler synchronized moves

Lock-up, bodyslam, Irish Whip, and break lock-up.

- **Combat owns positioning and timing.** On a successful lock-up, Combat snaps both wrestler positions to a pair anchor (initiator position plus a tuned offset, facing each other), sets LockUp / LockedUp, and raises `PairedMoveStarted(PairedMoveInfo { Initiator, Receiver, MoveId, Anchor, Facing, MoveTiming })`. During the move, Combat writes both root positions every tick (including where the receiver lands after a bodyslam and the whip run direction). Damage, Knocked Down, and the lock-up timer are applied by Combat at the times defined in `MoveTiming`.
- **Presentation owns visuals.** On `PairedMoveStarted`, each `WrestlerView` plays its side of the paired clip (Lock Up / Locked Up, Bodyslam / Bodyslammed, Irish Whip, Break) starting the same tick. Visual offsets inside the move (lifting the receiver overhead) are child-sprite animation within the clip, never root movement. Presentation may adjust sorting order for the pair while the move plays.
- Neither side waits for the other: Combat does not wait for animations to finish; presentation follows Combat's timing per 4.2.

## 7. Integration rules

- Only the Systems Architect edits `.unity` files and the Build Settings list. Owners deliver scripts and prefabs, plus a note on where they go; the Architect wires them.
- A prefab or Animator asset is edited only by its folder owner, and only one agent at a time. Work touching shared assets runs sequentially.
- With the Editor open, scenes and prefabs are changed through `unity command` (check `unity status` is `ready` first, pass `--project-path "C:\Users\theke\Rural Rumble"`). Never hand-edit `.unity` / `.prefab` YAML while the Editor is reachable.
- Changes to Core types, `CombatEvents`, `IWrestlerView`, `WrestlerIntent`, or this document go through the Architect. Update this document first, then the code.
- Input Actions, Project Settings, packages: Architect only. New packages need designer approval.
- Sequence for a feature: owner implements and compiles → Architect integrates into scenes → `unity command script recompile_status` and `unity command console get --level error` → QA runs tests → human playtest items listed.
- Every new asset gets an `ASSET_REGISTRY.csv` row (source, creator, license, date, human/AI/mixed/unknown origin). Placeholders are marked as placeholders.

## 8. Conflicts and gaps found

Reported, not resolved. Each needs a designer decision before the affected work.

Conflicts between documents:
1. **Lock-up target state.** CONTROLS.md: A triggers a lock-up "if close to *standing* opponent". COMBAT_SPEC allows it when Wrestler B is Standing, Walking, Striking (before the strike lands), or Recovering.
2. **Knockdown floor naming.** `qa-testing.md` says mashing "never below the minimum"; COMBAT_SPEC's floor is "Shortest possible knockdown" (0.5 s), separate from "Minimum knockdown time" (1.0 s, length at full health). Tests should use the 0.5 s value.
3. **AI state names.** GDD uses "Grapple" and "Stunned / Downed"; COMBAT_SPEC uses "Lock Up/Grapple" and "Stunned / Knocked Down". This document follows COMBAT_SPEC.
4. **Combat state list.** GDD MVP Scope lists seven states ("attacking", "grappling", ...); COMBAT_SPEC defines twelve. This document follows COMBAT_SPEC.
5. **Y = Pin.** GDD Controls table lists Y = Pin (stretch); CONTROLS.md does not. Not bound in MVP.
6. **Missing files.** CLAUDE.md lists `docs/ACCEPTANCE_TESTS.md` and `docs/ASSET_REGISTRY.csv` as sources of truth; neither exists yet. Q-004 refers to `.docx` files in `docs/`; none are there now.

Gaps (rules or values not specified):
1. Startup / active / recovery timing for each move (Combat adds starting points).
2. Match start: when the timer starts and whether fighting waits for the versus text; timer display format.
3. After the Victory screen: return to Start, restart, or quit; plus menu buttons (Q-002). Delay between knockout and the Victory screen.
4. Irish Whip: the whipper's state during the whip; whether the whipped wrestler can act or stops on its own; whether a strike on a whipped wrestler uses strike damage or Irish Whip impact damage.
5. Running: hold B or press once; which way to run when facing straight up or down.
6. Struck: how long it lasts and when the wrestler can act again.
7. Knockdown length: whether it scales linearly with health lost.
8. Whether the CPU mashes to shorten knockdowns.
9. Simultaneous lock-up attempts by both wrestlers.
10. Which arena edges are damaging obstacles and which are plain bounds (STYLE_GUIDE: "some of the items ... could be used").
11. Audio playback ownership: who plays combat SFX, footsteps, and music. Recommendation: Juice Agent plays SFX and music; Sound Wrangler provides files. **Proposed — needs designer approval.** Reason: keeps playback with other feedback that listens to combat events.
12. Character select: the CPU receiving the unpicked wrestler is implied, not stated.
13. Shift key bindings and the mash mechanic (Q-001).
