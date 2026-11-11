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
| `Assets/Audio/` (files and import settings); `Staging/Audio/` | Sound Wrangler (`.claude/agents/sound-wrangler.md`, D-034) |
| `Assets/Scripts/Juice/`, `Assets/Prefabs/Effects/` | Juice Agent (`.claude/agents/juice-agent.md`, D-034) |
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
| `RuralRumble.Match` | `Assets/Scripts/Match/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.AI`, `RuralRumble.Presentation` | `MatchBootstrap` only: builds the match from `GameSession` (D-022). Systems Architect. |
| `RuralRumble.UI` | `Assets/Scripts/UI/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.Match`, `Unity.InputSystem` | Screens, HUD, match flow. The HUD reaches the match through `MatchBootstrap.Current` (4.3), so UI references `RuralRumble.Match`; Match never references UI. |
| `RuralRumble.Juice` (created when first needed) | `Assets/Scripts/Juice/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.Match` | Feedback and audio playback only. Reaches the running match through `MatchBootstrap.Current` (like UI), so it references `RuralRumble.Match`; nothing references Juice. |
| `RuralRumble.Editor.Build` | `Assets/Editor/Build/` | runtime assemblies as needed | Editor platform only. |
| `RuralRumble.Editor.Tooling` | `Assets/Editor/Tooling/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.Presentation`, `Unity.2D.Sprite.Editor` | Editor platform only. Namespace `RuralRumble.EditorTooling` (a `.Editor` namespace segment would shadow `UnityEditor` lookups). `Unity.2D.Sprite.Editor` (2D Sprite package, already installed) gives the builder the sprite data provider API. |
| `RuralRumble.Tests.EditMode` | `Assets/Tests/EditMode/` | `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.AI`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `nunit.framework.dll` | Editor platform only; define constraint `UNITY_INCLUDE_TESTS`. |
| `RuralRumble.Tests.PlayMode` | `Assets/Tests/PlayMode/` | all runtime assemblies, `UnityEngine.TestRunner`; `nunit.framework.dll` | Any platform; define constraint `UNITY_INCLUDE_TESTS`. |

Dependency direction: Core ← Combat ← (AI, Presentation, UI, Juice). Combat never references Presentation, UI, AI, or Juice. Nothing references Tests.

Created so far: `RuralRumble.Core`, `RuralRumble.Combat`, `RuralRumble.Presentation`, `RuralRumble.Match` (Core, Combat, AI, Presentation), `RuralRumble.Tests.EditMode` (Core, Combat, AI), `RuralRumble.Tests.PlayMode` (Core, Combat, AI, Presentation, Match) from the movement slice; `RuralRumble.AI` (Core, Combat; holds a placeholder `AIAssembly.cs` until Combat State & AI adds `OpponentBrain`) and `RuralRumble.Editor.Tooling` (before this, `WrestlerAnimationBuilder` compiled into `Assembly-CSharp-Editor`) in combat-core step 1. `RuralRumble.UI` (Core, Combat, Match, InputSystem) was created for the Start screen (Assignment 5), and `RuralRumble.Tests.EditMode` now also references it. `RuralRumble.Editor.Build` (editor only, no references) holds `PlayFromStartScene`. `RuralRumble.Juice` is created when first needed.

**Scripts outside an asmdef.** `Assets/Animation/` holds assets only (clips, controllers, `MoveAnimationTable.asset`). Scripts there would compile into `Assembly-CSharp`, which no asmdef can reference, so runtime presentation types live in `Assets/Scripts/Presentation/` even when their assets live in `Assets/Animation/`. `MoveAnimationTable.cs` is in `Assets/Scripts/Presentation/` for this reason (confirmed, combat-core step 1).

## 3. Scenes and flow

Screen flow (D-003): **Start → CharacterSelect → Match → Victory**. No mode select.

| Build index | Scene | Contents |
|---|---|---|
| 0 | `Assets/Scenes/Start.unity` | Start screen UI |
| 1 | `Assets/Scenes/CharacterSelect.unity` | Character select UI |
| 2 | `Assets/Scenes/Match.unity` | Camera rig, Arena prefab (with `ArenaObstacleMarker`s), `MatchBootstrap`, `CombatMatchRunner`, HUD (later); wrestlers are spawned at runtime |
| 3 | `Assets/Scenes/Victory.unity` | Victory screen UI |

- **Loading:** `SceneLoader.Load(SceneId)` (Core) calls `SceneManager.LoadScene(name, LoadSceneMode.Single)`. Scene names live only in `SceneNames` constants. No persistent bootstrap scene, no additive loading, no Addressables. **Approved (D-021).** Reason: four small scenes need nothing more.
- UI & Match Flow calls `SceneLoader`; it never hard-codes scene names.
- **Editor Play starts at Start (designer request, 2026-11-11).** `Assets/Editor/Build/PlayFromStartScene.cs` (`RuralRumble.Editor.Build`, editor only) sets `EditorSceneManager.playModeStartScene` to the first enabled Build Settings scene, so pressing Play runs the full loop whatever scene is open. Toggle: **Rural Rumble > Play From Start Screen** (on by default; per-user EditorPrefs). Turn it off to play an open scene directly, e.g. Match.unity for combat testing. Builds are unaffected.
- `Match.unity` and the other screens are created new and the Build Settings list is replaced with the four scenes above. `SampleScene.unity` is deleted when the Architect creates the new scenes (designer authorized, D-021). **Status:** `Start.unity` (index 0) and `Match.unity` (index 1) are in Build Settings; CharacterSelect and Victory are added when built. Until CharacterSelect exists, the Start screen loads Match directly (designer decision, Assignment 5, 2026-11-11). UI Toolkit panels use `Assets/UI/RuralRumblePanelSettings.asset` (scale with screen size, 1920×1080) and theme `Assets/UI/RuralRumbleRuntimeTheme.tss`. `SampleScene.unity` is deleted.
- What follows the Victory screen is not defined (see Gaps).

### 3.1 Rendering scale (D-013)

- **Native resolution 960×540**, integer-scaled: ×2 at 1080p, ×3 at 1440p, ×4 at 4K.
- **Pixels per unit: 60** for all game sprites (wrestlers, arena, effects), so the screen is 16×9 world units. Barry (240 px) is 4.0 units tall; Peaux (215 px) about 3.6.
- **Match camera (D-017):** a plain orthographic camera driven by a `MatchCamera` component (Systems Architect, `Assets/Scripts/Core/`). Not URP's `PixelPerfectCamera`, which can't zoom smoothly.
  - **Default view:** orthographic size 4.5 (960×540 native, pixel-perfect at ×2 on 1080p). At this zoom, snap the camera position to 1/60 unit so pixels stay on the grid.
  - **Follow:** horizontally centers on the midpoint of the two wrestlers, clamped so the view never leaves the arena. Vertically framed on the walkable floor.
  - **Zoom:** when the wrestlers' horizontal separation would put either one past the screen edge margin, orthographic size grows so both stay in view, up to the size that shows the whole arena. It eases back to the default as they close in.
  - **Inspector values** (serialized fields on `MatchCamera`, editable in Play Mode): `defaultVisibleWidthFraction` = 0.6, `maxVisibleWidthFraction` = 1.0 (whole arena), `edgeMargin` (world units before zooming starts), `zoomSmoothTime`, `followSmoothTime`. Camera framing is presentation, not combat, so these are not in the combat tuning asset (D-005).
  - Follows the two wrestler root transforms, which Combat writes from `IWrestlerView.Position` (4.2); see "Camera rig (built)" below. Juice's camera shake stays on a child offset (4.2).
- **Arena dimensions:** the default view (960 px) is 60% of the arena width, so the arena is **1600 native px wide** (26.7 units). The walkable floor's full depth fits within the default 540-px-tall view. At full zoom-out the screen shows 1600×900, so the arena art is **1600×900 native px** (26.7×15 units): the extra height is sky and treeline above and foreground below, seen only when zoomed out.
- **Arena layout (native px, origin top-left of the 1600×900 canvas). Approved (D-018).** Art: `Assets/Art/Arena/arena_backdrop.png` (pivot top-left), `arena_truck.png` and `arena_pool.png` (pivot bottom-left, so a sprite's pivot sits at its left x and base row). 60 PPU: canvas x px ÷ 60 = world x; canvas row ÷ 60 = world y downward from the backdrop's top edge.
  - Default view vertical band: rows 300–840 (camera at default zoom is framed here).
  - Walkable feet range: rows 580 (back, in front of the trailer) to 820 (front); full width minus obstacles, narrowed at the left and right ends so a wrestler's whole sprite stays on the arena art (D-027; values in the table below). The camera is unchanged.
  - Layers, back to front: `backdrop` (sky, pines, trailer, floor; one image), then depth-sorted by base row (Sprite Sort Point = Pivot): truck (left x −40, base row 650, 502×300), pool (right edge, base row 640, 560×367), wrestlers. Anything with a base row below a wrestler's feet draws in front of that wrestler.
  - Obstacles: the trailer is the back wall (invisible collider along row 580). The truck and pool get colliders at their footprints. A run into the truck or pool puts the runner in Struck with obstacle collision damage, or Knocked Down if that leaves them below 30 health (D-030); the back wall and the arena ends only end the run (D-027). `ArenaObstacleMarker.MarkerRole.Obstacle` = damaging object; the `WalkableArea` edges = plain bounds.
- **World frame (built).** `Assets/Prefabs/Arena/Arena.prefab` sits at the world origin; the backdrop's top-left corner is (0, 0). Canvas (x px, row px) → world (x / 60, −row / 60), so world y is negative and grows toward the back. `Arena` children: `Backdrop` (order in layer −100, always behind), `Truck` at (−0.6667, −10.8333), `Pool` at (18.3333, −10.6667), both order 0 with Sprite Sort Point = Pivot. Wrestlers use order 0, Sort Point = Pivot (feet).
- **Arena rectangles (world units, for `ArenaObstacleMarker`s).** Footprint depths for the truck and pool are the Architect's reading of the art (back wall to the sprite's base row); Combat may adjust them, and playtesting should confirm them.

  | Rect | Canvas | World x | World y |
  |---|---|---|---|
  | Arena art | x 0–1600, rows 0–900 | 0 – 26.6667 | −15 – 0 |
  | Walkable feet band (D-027, built in combat-core step 4) | x 90–1510, rows 580–820 | 1.5 – 25.1667 | −13.6667 – −9.6667 |

  D-027 narrowing (the movement slice used the full width 0 – 26.6667). `WalkableBand` marker: offset (1.5, −13.6667), size (23.6667, 4). With `feetHalfWidth` 0.5 the feet point stops 2.0 units (120 px) from each art edge. That keeps the widest body poses on the art: a lying (Knocked Down / Knocked Out) body spans about ±2 units around the feet point (Animation & Tooling), stand/walk frames reach 74 px from the pivot and run frames 88 px. Only the extended arm of Strike 1 (146 px from the pivot) can still pass the edge, by about 26 px, when punching toward it; accepted as a playtest item. If `feetHalfWidth` changes, re-derive the band (band inset = 2.0 − `feetHalfWidth`).
  | Back wall (trailer) | row 580, full width | 0 – 26.6667 | y = −9.6667 (line); not a damaging obstacle (D-027) |
  | Truck footprint | x −40–462, rows 580–650 | −0.6667 – 7.7000 (0 – 7.7 inside the arena) | −10.8333 – −9.6667 |
  | Pool footprint | x 1100–1660, rows 580–640 | 18.3333 – 27.6667 (18.3333 – 26.6667 inside) | −10.6667 – −9.6667 |
- **Camera rig (built).** `Match.unity` root `MatchCamera` (component `MatchCamera`, moves) with child `Main Camera` (orthographic `Camera`, `UniversalAdditionalCameraData`, `AudioListener`). Juice shakes the child's local position. The default orthographic size is derived from `defaultVisibleWidthFraction` and the camera aspect (exactly 4.5 at 16:9); the maximum from `maxVisibleWidthFraction` (7.5 at 16:9, the whole 1600×900 arena). Vertical framing is centered on canvas row 570 (rows 300–840) at default zoom and is clamped to the arena art while zoomed out. Position is snapped to 1/60 unit only while the size equals the default. `MatchCamera.SetTargets(Transform, Transform)` takes the two wrestler roots; `CombatMatchRunner` writes those roots from `IWrestlerView.Position`, so the camera follows the same positions without referencing Combat.
- **Player settings (applied):** default window 1920×1080, Fullscreen Window.
- **Sprite import (applied to `Assets/Art/Wrestlers/Barry/barry_stand_front.png`):** Point filter, uncompressed, no mipmaps, PPU 60, pivot Bottom Center (feet), Full Rect mesh. Unity Animation & Tooling applies the same to every wrestler sprite and sheet it imports. Exception (D-032): the Lock Up / Locked Up pair sheets have their pivot 30 px toward the opponent from bottom-centre ((132 px art spacing − `lockUpPairSpacing` × 60) ÷ 2), so roots 1.2 units apart draw the hips 2.2 units apart; `SpriteRenderer.flipX` mirrors around the pivot, so this holds for either facing. The bodyslam pair sheets (D-033) get the same 30 px shift; the receiver sheets keep their drawn ground line as the pivot height (Peaux 4 px, Barry 1 px above the cell bottom). Max texture size is the smallest power of two that holds the sheet, at least 2048 (4096 for the bodyslam sheets), so no sheet is downscaled.
- UI canvases scale against 1920×1080 (UI & Match Flow's call on Canvas Scaler settings).

## 4. Seams

### 4.1 Input abstraction (player and CPU)

```csharp
// Core. One per wrestler. Combat calls Read() exactly once per combat tick.
public interface IWrestlerInput { WrestlerIntent Read(); }

public readonly struct WrestlerIntent {
    Vector2 Move;               // raw stick/WASD, each axis -1..1; Combat quantizes to 8 directions
    ButtonState LockUpGrapple;  // A / K
    ButtonState RunWhip;        // B / Space
    ButtonState Strike;         // X / J
}
public readonly struct ButtonState { bool Pressed; /* went down since last Read */ bool Held; }
```

- Intents are buttons, not actions. Combat decides what a press means in the current state (A = lock up or grapple; B = run, whip, or cancel a run the wrestler started themselves (D-027); Strike = combo strike, or Stomp when standing over a Knocked Down opponent (D-025); Strike/Grapple presses while Knocked Down = mash). This keeps CONTROLS.md logic in one place.
- **Run origin (D-027, D-028).** Combat records whether a run came from the wrestler's own Run press or from an Irish Whip. B cancels only an own run. A whipped run ignores all buttons for the whip no-control time, then B may cancel it (lock-up slice). The origin is internal combat state, not part of `IWrestlerView`.
- **CPU mashing (D-025).** While its own view is Knocked Down, `OpponentBrain` produces Strike presses at `CombatTuning` CPU mash rate. Combat applies them exactly like the player's presses; there is no CPU-only shortcut in Combat.
- `PlayerInputSource : MonoBehaviour, IWrestlerInput` (Core) reads the Input Actions asset. Combat never touches devices or `UnityEngine.InputSystem`.
- `OpponentBrain : IWrestlerInput` (AI, plain C#) produces the CPU's intents from read-only views (`IWrestlerView` of itself and the player) and `CombatTuning`. It evaluates on its decision interval, not every frame (GDD), and holds its last intent between evaluations. It cannot call combat methods directly.
  - **Time for the brain.** `Read()` takes no `dt` and AI must not read `Time`, so the brain gets a clock: `OpponentBrain(IWrestlerView self, IWrestlerView opponent, CombatTuning tuning, Func<float> clock)`, where `clock` returns combat seconds (`() => match.ElapsedSeconds` in the game, a test-controlled value in tests). The brain measures its decision interval and mash rate from clock differences between `Read()` calls. `ElapsedSeconds` advances inside `Tick`, after both `Read()`s, so the brain sees the previous tick's time; that one-tick lag is acceptable. The clock stops when the match ends. Exact constructor shape is Combat's call as long as it stays free of `Time` and MonoBehaviours.
  - `Read()` must be pure apart from the brain's own state (no combat calls), and `Pressed` must be true for only one `Read()` per press, like the player's adapter.
- Tests use a `ScriptedInput : IWrestlerInput` that returns queued intents.
- Input Actions asset: the template `Player` map is replaced by a `Wrestler` map with actions `Move`, `LockUpGrapple`, `RunWhip`, `Strike`, bound per CONTROLS.md and D-002 (keyboard per D-019). The template `UI` map is kept for menus; menu confirm/back per D-019. **Approved (D-021).**

### 4.2 Combat → presentation

Combat publishes; presentation, UI, and Juice subscribe. None of them change combat state, positions, health, or timing.

```csharp
// Combat, plain C#. Read-only per-wrestler view.
public interface IWrestlerView {
    WrestlerId Id;            // Player or Cpu (Core enum)
    CombatState State;        // exactly the 12 COMBAT_SPEC states
    MoveId CurrentMove;       // None, Strike1, Strike2, Strike3, Stomp (D-025), LockUp, Bodyslam, IrishWhip, BreakLockUp
    MovePhase Phase;          // None, Startup, Active, Recovery
    float Health; float MaxHealth;
    Vector2 Position;         // arena plane: x = left/right, y = depth
    Vector2 Facing;           // last 8-way direction; presentation derives left/right and front/back
    int HorizontalFacing;     // last left/right facing, +1 right / -1 left; kept while Facing is straight up/down (sprite flip, D-020 run direction)
}

// Combat, plain C#. One instance per match; all events fire inside CombatMatch.Tick.
// Raise methods are internal to RuralRumble.Combat.
public sealed class CombatEvents {
    event Action<WrestlerId, CombatState /*from*/, CombatState /*to*/> StateChanged; // not raised for a same-state restart
    event Action<WrestlerId, Vector2> FacingChanged;  // new 8-way Facing (movement slice); read HorizontalFacing for the flip
    event Action<WrestlerId, MoveId, MoveTiming> MoveStarted;      // move enters Startup; timing read from tuning now
    event Action<WrestlerId, MoveId, MovePhase> MovePhaseChanged;  // Startup -> Active -> Recovery -> None; an interrupted move goes to None
    event Action<PairedMoveInfo> PairedMoveStarted;   // lock-up, break (built, step 1); bodyslam (built, step 2); Irish Whip (step 3)
    event Action<HitInfo> HitLanded;                  // attacker, defender, move, damage, contact point; every hit, incl. Struck restarts
    event Action<WrestlerId, Vector2> ObstacleHit;    // running/whipped wrestler hits the truck or pool (contact point); not the back wall or ends
    event Action<WrestlerId, float> HealthChanged;    // new health (damage and Recovering regen)
    event Action<WrestlerId, float> KnockedDown;      // knockdown duration at the moment of the knockdown, before mashing
    event Action<MatchResult> MatchEnded;             // winner, loser, elapsed seconds; raised once
}

// Combat. Serializable so CombatTuning holds one per move (Inspector-editable, D-005).
[Serializable] public struct MoveTiming { public float Startup, Active, Recovery; float Total; }
// Combat.
public readonly struct HitInfo { WrestlerId Attacker, Defender; MoveId Move; float Damage; Vector2 ContactPoint; }
// Combat (built, lock-up step 1). Field names follow HitInfo (Move, Timing) rather than section 6's type names.
public readonly struct PairedMoveInfo { WrestlerId Initiator, Receiver; MoveId Move; Vector2 Anchor; int Facing; MoveTiming Timing; }
//   Anchor = midpoint of the two snapped feet points; Facing = initiator's left/right (+1/-1), receiver faces the other way.
```

`MoveId` values are explicit (`None = 0` … `Strike3 = 3`, `Stomp = 4`, `LockUp = 5`, `Bodyslam = 6`, `IrishWhip = 7`, `BreakLockUp = 8`) because they are serialized in the Animator's `Move` parameter and in `MoveAnimationTable`. Append new values; never renumber.

**Combat-core slice rules that shape the seams (D-024, D-025, D-027):**
- **Struck restarts on every hit** (D-024). Because the state does not change, `StateChanged` does not fire; presentation restarts the Struck clip when `HitLanded` names its wrestler as defender. Strike 3 sends the defender to Knocked Down.
- **Strikes can land on Struck** (combos connect) **and on Knocked Down, Stomp only** (D-025). A Stomp is a normal move: `CurrentMove = Stomp`, attacker state Striking, `MoveStarted`/`MovePhaseChanged`/`HitLanded` as for strikes. It deals damage only, does not change the knockdown timer, and can knock out. The Stomp cooldown starts when the stomp's Recovery ends; it is internal combat state (no event, not in `IWrestlerView`). Whether Strike means Stomp is decided by Combat at the press (stomp range is a tuning value).
- **No movement while Striking** (D-024). Strike is allowed while running: it ends the run and throws Strike 1 on the spot. Lock Up is not allowed while running (D-029).
- **Knocked Down** length is linear in health lost (D-025). Mashing shortens the remaining time; `KnockedDown` carries only the starting duration. No invulnerability after getting up.
- **Knocked Out** (health 0) raises `MatchEnded` once (both wrestlers at 0 on the same tick = draw, D-030), then the match freezes: `CombatMatch.Tick` changes nothing, `ElapsedSeconds` stops, and Combat exposes `CombatMatch.IsOver` (bool) and `CombatMatch.Result` (`MatchResult`, valid when `IsOver`). The runner keeps calling `Tick`. The ~1.5 s hold and the Victory load are UI's (D-026, step 7).
- **Recovering** walks slowly, cannot run, and regenerates health only while standing still, up to the recovery cap. Presentation picks the Recovering stand or walk pose from whether `Position` changed since its last frame (visual choice only, no rule).

`CombatState` values: Standing, Walking, Running, Striking, Struck, LockUp, LockedUp, Grappling, Grappled, KnockedDown, Recovering, KnockedOut. Do not add states without a COMBAT_SPEC change.

**Wrestler objects.** A wrestler prefab (`Assets/Prefabs/Wrestlers/`) is presentation only: root with `WrestlerView` (Presentation), child `SpriteRenderer`, `Animator`. It holds no combat components. Combat's scene-level `CombatMatchRunner` writes each wrestler root's `transform.position` from `IWrestlerView.Position` every tick; presentation only animates children. **Approved (D-021).** Reason: one owner per prefab and per transform; no two agents edit the same prefab.

Both wrestlers share one Animator controller; each wrestler gets an `AnimatorOverrideController` with its own clips. **Approved (D-021).** Reason: one state graph matches the shared combat framework. Each wrestler still has its own clips for every state (their own look and timing); only wrestler-exclusive moves would need new states, which is outside the MVP.

**Attack timing.** Combat is the authority for when a hit can land.
- Every move has a `MoveTiming { float Startup; float Active; float Recovery; }` (seconds) in `CombatTuning`. Combat checks for contact only during `Active`.
- `MoveStarted` carries the current `MoveTiming`. Presentation looks up the clip in a `MoveAnimationTable` (type `Assets/Scripts/Presentation/MoveAnimationTable.cs`, asset `Assets/Animation/MoveAnimationTable.asset`, both owned by Animation & Tooling; see section 2 for why the type is not in `Assets/Animation/`) that records each clip's contact time at native speed, and sets playback speed so the contact frame lands at `Startup`. `MovePhaseChanged` lets presentation resync. `WrestlerView` gets the table through a serialized field on the wrestler prefab.
- Strike starting timings match the approved art's contact frames (D-024): Strike 1/2 Startup 0.21, Active 0.1, Recovery 0.15 s; Strike 3 0.125 / 0.1 / 0.25 s. Stomp has no art yet; its timing is a Combat starting point and its clip is a stand-in until the Stomp animation exists.
- Animation events never trigger hits or state changes. If a clip cannot match the timing, Animation & Tooling reports it; it does not change tuning.
- An Editor validator in `Assets/Editor/Tooling/` reports moves whose clip contact time and tuned `Startup` would require extreme playback speeds.
- **Approved (D-021).** Reason: keeps hit detection testable without Animators, and Play Mode tuning edits stay in sync because speed is computed at each move start.

**Hit detection.** Plain C# box overlap on the arena plane (`ArenaRect` hurt/hit boxes with a depth tolerance), computed in `CombatMatch.Tick`, not Physics2D colliders or triggers. Box sizes and ranges are tuning values. Both wrestlers are resolved in one tick, so "first strike to land wins" is deterministic. Arena bounds and obstacles are `ArenaRect`s collected from `ArenaObstacleMarker` components (Combat) on the arena prefab at match start. **Approved (D-021).** Reason: EditMode-testable, deterministic, no physics callback ordering.

**Wrestler blocking (D-027, D-029, D-030).** The two wrestlers' blocking boxes (`feetHalfWidth` × `wrestlerBlockHalfDepth`, default 0.25; `feetHalfDepth` is only for the arena bounds) never overlap. Exceptions: a running wrestler passes over an opponent who is Knocked Down or Knocked Out (walking is still blocked by a downed wrestler), and a runner who reaches a standing opponent knocks both down, each with their normal knockdown and `runCollisionDamage` (default 0) (D-029, D-030). `CombatMatch` resolves this after movement in the same tick, in plain C# like the arena bounds: a move that would overlap is stopped at contact (no pushing). The resolution must not depend on which wrestler is ticked first in a way that favors the player; Combat documents its order. Paired moves (lock-up slice) place wrestlers by anchor and are exempt while they play. Hit boxes are separate from feet boxes, so strikes still reach a blocked opponent.

**Walking speed (D-029, replaces D-027's "same speed").** Three Inspector values: `walkSpeedHorizontal` (3.0), `walkSpeedDepth` (2.0; means vertical, toward/away from the camera), `walkSpeedDiagonal` (3.6, total speed along a diagonal).

**Tick.** `CombatMatchRunner.Update` calls `CombatMatch.Tick(dt, playerIntent, cpuIntent)` with `Time.deltaTime` clamped to a maximum step. Rules never read `Time`. **Approved (D-021).** Reason: simplest loop with no lost button presses; tests pass explicit `dt`.

**Depth sorting.** The 2D renderer sorts by Y (`Renderer2D.asset` transparency sort mode Custom Axis, axis 0,1,0; the mode was Default until the movement slice). Wrestlers and arena props use the same sorting layer and order 0, with Sprite Sort Point = Pivot.

**Where the shared types live.** `IWrestlerView`, `WrestlerId`, `CombatState`, `MoveId`, `MovePhase` and `MatchResult` are in Core (`Assets/Scripts/Core/`), because Core consumers (`MatchCamera`, `GameSession`) and every higher assembly need them and Core cannot reference Combat. Combat implements `IWrestlerView`; `CombatEvents`, `MoveTiming`, `HitInfo` and `PairedMoveInfo` stay in Combat.

**Juice.** Subscribes to `HitLanded`, `ObstacleHit`, `KnockedDown`, `MatchEnded`. Never sets `Time.timeScale` or changes combat. Hitstop would need a pause hook in `CombatMatchRunner` that the Architect adds only with designer approval. Camera shake applies to a child offset of the camera, not the camera the Architect places.

### 4.3 Combat → UI

UI reads `IWrestlerView.Health / MaxHealth` (bars only, never numbers), subscribes to `HealthChanged` and `MatchEnded`, and reads `CombatMatch.ElapsedSeconds` for the timer. `MatchResult { WrestlerId Winner; WrestlerId Loser; float ElapsedSeconds; bool IsDraw; static MatchResult Draw(float elapsedSeconds); }` (Core, `Assets/Scripts/Core/Session/MatchSetup.cs`). `IsDraw` is true for a double knockout on one tick (D-030); then `Winner`/`Loser` carry no meaning and UI shows the draw message (Victory screen, UI step). Added by Combat State & AI with the coordinator's approval in combat-core step 2.

- The match timer counts up (STYLE_GUIDE) and stops at knockout. Combat owns it because it is match state.
- On `MatchEnded`, UI's `MatchFlow` stores the result in `GameSession.LastResult` and loads Victory. Victory builds "Victory! {Winner} knocked out {Loser} in {timer}" plus "Congratulations!" or "Game Over!" from `MatchResult` and the definitions in `GameSession.Setup`.
- `MatchBootstrap` exposes the `CombatMatch` to the HUD (`MatchBootstrap.Current.Match`; `Current` is set in `Awake` and cleared in `OnDestroy`; `Match` is non-null from default-order `Start` onward, see 4.4). UI must not find wrestlers by name or tag.
- **UI system: UI Toolkit** (UXML/USS in `Assets/UI/`, one `UIDocument` per screen prefab in `Assets/Prefabs/UI/`). **Approved (D-021).** Reason: layouts are plain text that agents can write and review without editing prefab YAML. UI & Match Flow still runs the `unity:ui` skill before starting.

### 4.4 Character selection → match

```csharp
// Core
public sealed class WrestlerDefinition : ScriptableObject {
    string DisplayName; Sprite Portrait; GameObject Prefab; // Prefab = presentation prefab
    string FullName; string Tagline; Sprite SelectCard;     // Character Select (Assignment 5); FullName falls back to DisplayName
    IReadOnlyList<string> VictoryLines;                     // Victory screen gloat lines (Assignment 4 output, designer-approved)
}
public sealed class WrestlerRoster : ScriptableObject { WrestlerDefinition[] Wrestlers; } // exactly 2 in MVP
public readonly struct MatchSetup { WrestlerDefinition Player; WrestlerDefinition Cpu; }
public static class GameSession { MatchSetup Setup; MatchResult LastResult; bool HasSetup; }
```

- Assets: `Assets/Data/Wrestlers/Barry.asset`, `Peaux.asset`, `WrestlerRoster.asset` (Barry = entry 0, Peaux = entry 1). Definitions hold no combat stats; both wrestlers share `CombatTuning` (D-005).
- `Peaux.asset`'s prefab is `Peaux.prefab` (built by `WrestlerAnimationBuilder.BuildPeaux`). Peaux has real Standing Front and Walking Front clips; his other slots use `_PLACEHOLDER` stand-ins until his art exists. Portraits are empty until UI art exists.
- CharacterSelect sets `GameSession.Setup` (player's pick; CPU gets the other) and loads Match.
- `MatchBootstrap` (`RuralRumble.Match`, D-022; in Match scene) reads `GameSession.Setup`, instantiates both prefabs, creates `PlayerInputSource` and (later) `OpponentBrain`, and configures `CombatMatchRunner`, which builds the `CombatMatch`. If `HasSetup` is false (Match opened directly in the Editor), it uses roster entry 0 as player and 1 as CPU, or the reverse when its `directPlaySwapWrestlers` testing toggle is on (on in Match.unity as of 2026-10-31, so the designer plays Peaux against a CPU Barry; it has no effect once Character Select sets `GameSession.Setup`).
- **Bootstrap order (built).** `MatchBootstrap.Awake`: resolve the setup, instantiate both prefabs at the spawn feet points (serialized; player (9.0, −11.6667), CPU (17.6667, −11.6667)), add `PlayerInputSource` to the bootstrap object, call `CombatMatchRunner.Configure(playerRoot, cpuRoot, playerInput, cpuInput)` (CPU input = an `InputRelay` that `Start` points at the `OpponentBrain`, see below). All Awakes precede all Starts. `CombatMatchRunner.Start` (order −50) builds the match from the roots' positions and the Arena's markers. `MatchBootstrap.Start` (order −40) binds each `WrestlerView` to `Match.Player` / `Match.Cpu` and calls `MatchCamera.SetTargets`. Each frame: runner `Update` (−50) ticks and writes roots, `WrestlerView.Update` (0) animates, `MatchCamera.LateUpdate` follows. `MatchBootstrap.Match` is valid from default-order Starts onward.
- **CPU brain wiring (built, combat-core step 4).** `OpponentBrain` needs the two `IWrestlerView`s and the tuning, which come from the `CombatMatch` built in `CombatMatchRunner.Start`, but `Configure` runs in `Awake`. `MatchBootstrap.Awake` passes an `InputRelay` (internal `IWrestlerInput` in `MatchBootstrap.cs`) as the CPU input. `MatchBootstrap.Start` creates `new OpponentBrain(match.Cpu, match.Player, match.Tuning, () => match.ElapsedSeconds)`, sets it as the relay's source, and exposes it as `MatchBootstrap.CpuBrain`. The relay reads idle until then; no tick happens before Start. (The brain's `(tuning, clock, seed)` + `Bind` constructor would also work, but needs the tuning in `Awake`; the relay keeps `CombatMatch.Tuning` the only tuning reference.) `MatchBootstrap.Start` also calls `WrestlerView.Bind(view)` and `WrestlerView.BindEvents(match.Events, id)` for each wrestler.
- `GameSession` resets in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method so Play Mode without domain reload starts clean.
- **Approved (D-021).** Reason: a static holder is the simplest way to cross one scene load; no DontDestroyOnLoad objects to manage.

### 4.5 Testability

- All rules live in plain C# in `RuralRumble.Combat` and `RuralRumble.AI`: `CombatMatch`, `WrestlerStateMachine`, `ComboTracker`, `LockUpController`, `HealthModel`, `KnockdownTimer`, `ArenaGeometry`, `OpponentBrain`. Names are guidance; the split is Combat's call.
- No rule class reads `Time`, `Input`, scenes, or MonoBehaviours. Time arrives as `dt`; input as `WrestlerIntent`; tuning as a `CombatTuning` reference.
- MonoBehaviours (`CombatMatchRunner`, `ArenaObstacleMarker`) only connect rules to Unity.
- EditMode tests build a `CombatMatch` with `ScriptedInput`, a `CombatTuning` instance (`ScriptableObject.CreateInstance<CombatTuning>()` gives COMBAT_SPEC defaults, or load the project asset), and arena rects. Assertions read thresholds and timings from that instance.
- PlayMode tests cover scene flow and the runner.

## 5. Combat tuning asset

- Type: `CombatTuning : ScriptableObject` in `Assets/Scripts/Combat/Tuning/`. Asset: `Assets/Data/Tuning/CombatTuning.asset`. One asset shared by both wrestlers and the CPU.
- **Built so far:** movement and combat-core fields. Movement: `walkSpeedHorizontal`, `walkSpeedDepth` (vertical walk speed), `walkSpeedDiagonal` (D-029), `recoveringWalkSpeedMultiplier`, `runSpeed`, `moveDeadzone`, `feetHalfWidth`, `feetHalfDepth` (arena bounds), `wrestlerBlockHalfDepth` (wrestler-vs-wrestler blocking), `maxTickStep`. Combat-core: the Timing, Health and Damage (incl. `runCollisionDamage`, D-029), Recovery and Knockdown groups below, strike reaches and hurt box, stomp range, and the CPU values. Lock-up step 1: `lockUpTimer` (2.0), `lockUpWhiffTime` (0.4, D-031), `lockUpReach` (1.0), `lockUpPairSpacing` (1.2), `breakLockUpTiming` (0.2 / 0 / 0.2), `cpuLockUpChance` (0.25). Lock-up step 2 (bodyslam): `bodyslamDamage` (12, COMBAT_SPEC Starting point), `bodyslamTiming` (1.15 / 0.1 / 1.11; Startup is the fallback for an initiator with no entry, Active + Recovery = the rest of the approved clips after impact; Combat starting points), `bodyslamStartupByInitiator` (`WrestlerStartup { WrestlerDefinition wrestler; float startup; }[]`; project asset: Barry 1.208, Peaux 1.083, Set by the D-033 art timing), `cpuBodyslamChance` (0.5, Starting point). Whip values come with step 3.
- Rules hold a reference to the asset and read fields at the moment of use. Never copy values into fields in constructors, `Awake`, or `Start`. Inspector edits in Play Mode apply on the next use.
- Edits to a ScriptableObject in Play Mode persist after exiting Play Mode. Agents must not reset or overwrite tuned values in the asset; field initializers carry the COMBAT_SPEC defaults and apply only to new instances.
- Fields, grouped as in COMBAT_SPEC Tuning Values:
  - Timing: combo window (0.5 s), lock-up timer (2.0 s), Struck duration (0.4 s, D-024), `MoveTiming` for Strike 1, 2, 3 (D-024 values, 4.2) and Stomp, whip no-control time (3.0 s, D-028; lock-up slice).
  - Health and Damage: max health (100), Recovering threshold (30), Strike 1/2/3 damage (3/4/6), Stomp damage (3, D-025), Stomp cooldown (1.0 s, D-025), bodyslam damage (12), Irish Whip impact damage (8), obstacle collision damage (8, truck and pool only, D-027).
  - Recovery and Knockdown: recovery rate (1/s), recovery cap (30), minimum knockdown time (1.0 s), maximum knockdown time (3.0 s), mash reduction per press (8%), shortest possible knockdown (0.5 s), CPU mash rate (3 presses/s, D-025).
  - Combat-core slice adds Timing, Health and Damage, and Recovery and Knockdown fields except the lock-up, bodyslam, Irish Whip and whip values, which come with the lock-up slice.
- Values not in COMBAT_SPEC that Combat adds as starting points, each reported for designer review: `MoveTiming` for moves other than strikes, walk/run speeds, Recovering walk-speed multiplier, hit/hurt box sizes, stomp range, lock-up range, paired-move positioning offsets, CPU decision interval, ranges, and action probabilities (GDD: "configurable probabilities").
- Rules (what is allowed, what happens) are never added to the asset as settings to avoid a design decision.

## 6. Two-wrestler synchronized moves

Lock-up, bodyslam, Irish Whip, and break lock-up.

**Built (lock-up slice step 1): lock-up, whiff, break.** `LockUpController` (Combat, plain C#), run by `CombatMatch.Tick` right after both wrestlers think:
- A Lock Up press from Standing, Walking or Recovering is resolved after both think, against the opponent's state that tick. Valid target: Standing, Walking, Recovering, or Striking with a combo strike still in Startup (`LockUpController.CanBeLockedUp`); a wrestler who pressed Lock Up on the same tick counts as valid. Reach is the strike test with `lockUpReach` against the hurt box, depth within `strikeDepthTolerance`. Both pressing and both in reach: 50/50 (D-028) from the `random01` source passed to `CombatMatch` (time-seeded `System.Random` when null; tests inject a fixed source). Only one in reach: that one wins.
- Success: the receiver's strike (if any) is cancelled (`MovePhaseChanged` None), the initiator stays put and the receiver is snapped `lockUpPairSpacing` in front at the initiator's depth (if that spot is outside the arena or in the truck/pool, the initiator is placed behind the receiver instead; if both fail, neither moves), both face each other, A = LockUp, B = LockedUp, `CurrentMove` = LockUp for both, then `PairedMoveStarted(LockUp)`. The LockUp move's timing is (0, `lockUpTimer`, 0): Active is the window for Grapple / Irish Whip. When it ends: `PairedMoveStarted(BreakLockUp)` with `breakLockUpTiming`; when that ends both return to Standing or Recovering by health. Movement and blocking are skipped while the pair plays.
- Paired moves raise `PairedMoveStarted` instead of `MoveStarted`; `MovePhaseChanged` fires for both wrestlers (LockUp Active, Recovery, None; BreakLockUp Active, Recovery, None).
- During the lock-up B ignores every input; A's Strike and movement are ignored (D-031). A's Grapple and Irish Whip presses reach `LockUpController.OnGrapplePressed` / `OnIrishWhipPressed` anywhere in the lock-up window (from the first tick after the lock-up); Grapple starts the bodyslam (step 2), Irish Whip does nothing until step 3.

**Built (lock-up slice step 2): bodyslam.**
- Grapple in the lock-up window: A = Grappling, B = Grappled, `CurrentMove` = Bodyslam for both, positions unchanged (no snap, no root movement for the whole move, D-033), then `PairedMoveStarted(Bodyslam)` whose `Timing` is the initiator's: `CombatTuning.BodyslamTimingFor(initiator's WrestlerDefinition)` = `bodyslamTiming` with Startup replaced by the initiator's `bodyslamStartupByInitiator` entry (Barry 1.208, Peaux 1.083 in the project asset), so each pair's clips play at native speed. `MovePhaseChanged`: LockUp None, then Bodyslam Active, Recovery, None for both.
- Impact = end of Startup (Bodyslam Active starts): `bodyslamDamage` (12) to B, `HitLanded(Bodyslam)` (contact point = B's feet) and `HealthChanged`. B stays Grappled so the Bodyslammed clip keeps playing. If the damage takes B to 0, B is Knocked Out at once and the match ends that tick (A stays Grappling over the frozen match, so its clip plays out; B's Animator leaves the Bodyslammed clip for Knocked Out).
- End of Recovery (Active 0.1 + Recovery 1.11 = the 1.21 s left in both clips after impact): B Knocked Down (`KnockedDown` with the health-based length), A Standing or Recovering by health. Neither wrestler has control during the bodyslam. The receiver is not repositioned at the end: its root stays 1.2 from A while the lying body is drawn 1.1-1.5 units beyond it, and the Knocked Down clip is still a stand-in (reported to Animation & Tooling / the designer, not decided by Combat).
- Per-wrestler entries need to know who is who: `CombatMatch` takes optional `playerWrestler` / `cpuWrestler` `WrestlerDefinition`s, passed through new optional parameters on `CombatMatchRunner.Configure(..., playerDefinition, cpuDefinition)`. `MatchBootstrap.Awake` passes `setup.Player, setup.Cpu` to `Configure`. A wrestler with no `bodyslamStartupByInitiator` entry falls back to `bodyslamTiming.Startup` (1.15 s).
- CPU: holding a lock-up it started, `OpponentBrain` presses Grapple at `cpuBodyslamChance` (0.5) per decision tick, the first roll one decision interval in; it gives no input while Grappling or Grappled.
- Whiff (D-031): no valid target in reach. One-wrestler move: state LockUp, `CurrentMove` LockUp, `MoveStarted(LockUp, (0, lockUpWhiffTime, 0))`, no `PairedMoveStarted`, rooted, can be struck (strikes land on Lock Up), then Standing or Recovering. Presentation tells a whiff from a lock-up by the missing `PairedMoveStarted` (or the opponent not being Locked Up); `CombatWrestler.IsWhiffing` exposes it for tests.

- **Combat owns positioning and timing.** On a successful lock-up, Combat snaps both wrestler positions to a pair anchor (initiator position plus a tuned offset, facing each other), sets LockUp / LockedUp, and raises `PairedMoveStarted(PairedMoveInfo { Initiator, Receiver, MoveId, Anchor, Facing, MoveTiming })`. During the move, Combat writes both root positions every tick (including where the receiver lands after a bodyslam and the whip run direction). Damage, Knocked Down, and the lock-up timer are applied by Combat at the times defined in `MoveTiming`.
- **Presentation owns visuals.** On `PairedMoveStarted`, each `WrestlerView` plays its side of the paired clip (Lock Up / Locked Up, Bodyslam / Bodyslammed, Irish Whip, Break) starting the same tick. Visual offsets inside the move (lifting the receiver overhead) are child-sprite animation within the clip, never root movement. Presentation may adjust sorting order for the pair while the move plays. Lock Up / Locked Up play once (f1 0.333 s, f2 0.542 s, then f3 held until the lock-up ends) at native speed; whiff and break are still stand-ins (D-032). Bodyslam (D-033): Grappling + Move Bodyslam plays Bodyslam (initiator), Grappled + Move Bodyslam plays Bodyslammed (receiver), once, last frame held, initiator drawn in front. Both clips have a MoveAnimationTable entry whose contact time is the impact frame (receiver flat on the ground), so presentation plays them at impact time ÷ Bodyslam Startup: Barry slams Peaux 2.417 s clip, impact 1.208 s (frame 6 of 7); Peaux slams Barry 2.292 s, impact 1.083 s (frame 5 of 6). The art assumes both roots stay where the lock-up left them (`lockUpPairSpacing` apart) for the whole clip; the receiver's lying body is drawn behind its root (away from the initiator), so any receiver root movement Combat applies during the clip moves the drawn body by the same amount (flagged to Combat, not decided here).
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
1. ~~**Lock-up target state.**~~ Resolved (D-028): COMBAT_SPEC's list; CONTROLS.md corrected. CONTROLS.md: A triggers a lock-up "if close to *standing* opponent". COMBAT_SPEC allows it when Wrestler B is Standing, Walking, Striking (before the strike lands), or Recovering.
2. **Knockdown floor naming.** `qa-testing.md` says mashing "never below the minimum"; COMBAT_SPEC's floor is "Shortest possible knockdown" (0.5 s), separate from "Minimum knockdown time" (1.0 s, length at full health). Tests should use the 0.5 s value.
3. **AI state names.** GDD uses "Grapple" and "Stunned / Downed"; COMBAT_SPEC uses "Lock Up/Grapple" and "Stunned / Knocked Down". This document follows COMBAT_SPEC.
4. **Combat state list.** GDD MVP Scope lists seven states ("attacking", "grappling", ...); COMBAT_SPEC defines twelve. This document follows COMBAT_SPEC.
5. **Y = Pin.** GDD Controls table lists Y = Pin (stretch); CONTROLS.md does not. Not bound in MVP.
7. ~~**MatchBootstrap's assembly.**~~ Resolved (D-022): `MatchBootstrap` moves (with its .meta, so the scene reference survives) to a new Architect-owned `RuralRumble.Match` assembly in `Assets/Scripts/Match/`, done in movement-slice step 4.
8. ~~**Striking from Running.**~~ Resolved (D-029): Strike allowed while running (ends the run, Strike 1); Lock Up not. D-024 says "Striking ends a run (D-020: another action)", which implies a running wrestler can strike. COMBAT_SPEC Striking/Combo Rules lists Running as a valid state for Wrestler B only (the target), not for the striker, and the Lock Up list doesn't allow Running either. Until the designer decides, it is unclear which buttons, if any, a running wrestler can use besides B (cancel). Found in combat-core step 1.
9. ~~**"Same speed in every direction" vs. separate depth speed.**~~ Resolved (D-029): three tunable walk speeds (horizontal 3.0, vertical 2.0, diagonal 3.6). D-027 says walking is the same speed in every direction. The movement slice has separate horizontal (3.0) and depth (2.0) walk speeds, which the designer playtested. Read literally, D-027 means one walk speed for all eight directions; read narrowly ("diagonals are no longer faster"), it means only that a diagonal must not exceed the straight speeds while horizontal and depth speeds may still differ. Found in combat-core step 1.
6. **Missing files.** CLAUDE.md lists `docs/ACCEPTANCE_TESTS.md` and `docs/ASSET_REGISTRY.csv` as sources of truth; neither exists yet. Q-004 refers to `.docx` files in `docs/`; none are there now.

Gaps (rules or values not specified):
1. ~~Move timing~~ Strikes resolved (D-024); grapple timings are still Combat starting points.
2. ~~Match start~~ Resolved (D-026). When the timer starts and whether fighting waits for the versus text; timer display format.
3. ~~After the Victory screen~~ Resolved (D-026). return to Start, restart, or quit; menu buttons are decided (D-019). Delay between knockout and the Victory screen.
4. ~~Irish Whip~~ Resolved (D-028): whipped wrestler has no control for 3 s, then may cancel with Run; the whipper regains control when the whip animation ends; strikes on a whipped wrestler deal strike damage. The whipper's state during the whip; whether the whipped wrestler can act or stops on its own; whether a strike on a whipped wrestler uses strike damage or Irish Whip impact damage.
5. ~~Running~~ Resolved (D-020): one press runs until an obstacle, the arena edge, a hit, or another action; facing up/down runs toward the last left/right facing.
6. ~~Struck~~ Resolved (D-024): 0.4 s; strikes can land on Struck. How long it lasts and when the wrestler can act again.
7. ~~Knockdown length~~ Resolved (D-025): linear. whether it scales linearly with health lost.
8. ~~CPU mashing~~ Resolved (D-025): yes, tunable rate. Whether the CPU mashes to shorten knockdowns.
9. ~~Simultaneous lock-up~~ Resolved (D-028): random 50/50.
10. ~~Damaging edges~~ Resolved (D-027): truck and pool damage a runner; trailer/fence line and arena ends don't. Which arena edges are damaging obstacles and which are plain bounds (STYLE_GUIDE: "some of the items ... could be used").
11. ~~Audio playback ownership~~ Resolved (D-021): Juice Agent plays SFX and music; Sound Wrangler provides files.
12. ~~Character select~~ Resolved (D-021): the CPU always gets the wrestler the player didn't pick.
13. ~~Shift key bindings~~ Resolved (D-019): no Shift keys; Strike = J, Lock Up/Grapple = K.
14. ~~**Running into the other wrestler.**~~ Resolved (D-029, D-030): knocks both down (run-collision damage, default 0); runners pass over downed wrestlers. D-027 makes wrestlers block each other, but a run ends only at an obstacle, the arena edge, a hit, another action, or a second Run press (D-020, D-027). What happens when a runner reaches the opponent (run ends with no damage like an arena end, the runner stays Running in place, or something else) is not specified.
15. ~~**Knockdown check: health before or after the hit.**~~ Resolved (D-029): after damage. COMBAT_SPEC Knocked Down lists "a strike is performed on the Wrestler when below 30 points of health", while the striking process applies damage first and then says the wrestler "may enter Knocked Down if their health is below the Recovering threshold". The second reading (check after damage, so the hit that takes health from 32 to 28 knocks down) follows the written order; the first would not knock down on that hit. Confirm.
