# Rural Rumble Acceptance Tests

Completion criteria for each feature, written from the design documents (GDD, COMBAT_SPEC, CONTROLS, STYLE_GUIDE, ARCHITECTURE, DECISION_LOG). Owned by QA & Testing.

- Tests follow the spec, not the code. When they disagree, the test keeps the spec and QA reports the mismatch.
- Thresholds, speeds and timings are read from `CombatTuning` in tests, never hard-coded. The exceptions are the layout numbers ARCHITECTURE states (arena rects, spawn points, camera sizes).
- Passing tests do not prove the game feels right. Every feature lists the human playtest checks it still needs.

How to run (Editor open, `unity status` = ready):

```
unity command test run --mode editmode --async_tests true --project-path "C:\Users\theke\Rural Rumble"
unity command test run --mode playmode --async_tests true --project-path "C:\Users\theke\Rural Rumble"
unity command test status --project-path "C:\Users\theke\Rural Rumble"
```

Run both suites with `--async_tests true` and poll `test status` until it says "completed" (EditMode first, then PlayMode). A synchronous `--mode PlayMode` call reported 0 tests. A newly written test file may not be imported until the Editor refreshes; if a new test is missing from the count, refresh (`unity command script eval --code "UnityEditor.AssetDatabase.Refresh(); return 1;"`) and run again.

Legend: **Auto** = covered by an automated test. **Human** = needs playtesting. **Gap** = the spec doesn't define it, so the test checks the current behavior and the question goes to the designer.

---

## Movement Slice

Scope: Standing, Walking and Running only (COMBAT_SPEC State Definitions; D-001, D-020), the walkable band and obstacles (ARCHITECTURE 3.1, D-018), the match camera (D-017), and Match.unity assembly (ARCHITECTURE 4.4, D-022).

Test files: `Assets/Tests/EditMode/Movement/*.cs` (helpers in `Assets/Tests/EditMode/Support/`) and `Assets/Tests/PlayMode/*.cs`.

Last run: 2026-10-31 (with the combat-core slice; see that section). Movement tests all pass. Rows were updated for D-027 (narrowed band, blocking, obstacle damage) and D-029 (three walk speeds), and for the CPU brain that replaced the idle stand-in.

### M1. Eight-way walking (D-001, CONTROLS.md)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M1.1 | The stick maps to exactly 8 directions; each 45° sector snaps to its nearest direction | D-001, CONTROLS | `MoveDirectionTests.Quantize_FullStick_*`, `Quantize_AngledStick_*` | Auto |
| M1.2 | Stick inside the deadzone (`moveDeadzone`, radial) = no direction; the wrestler stays Standing | ARCHITECTURE 4.1, 5 | `MoveDirectionTests.Quantize_InsideDeadzone_*`, `Quantize_JustOutsideDeadzone_*`, `Wrestler_StickInsideDeadzone_StaysStanding` | Auto |
| M1.3 | Horizontal walking moves at `walkSpeedHorizontal`, depth at `walkSpeedDepth` | ARCHITECTURE 5 | `WalkingTests.Walk_Horizontal_*`, `Walk_Depth_*` | Auto |
| M1.4 | Diagonals move at `walkSpeedDiagonal` along the horizontal:vertical speed ratio (three separate walk speeds) | D-029 | `WalkingTests.Walk_Diagonal_UsesDiagonalSpeedAlongAxisRatio`, `Walk_Diagonal_SpeedIsTunable`; defaults in `TuningDefaultsTests` | Auto |
| M1.5 | Releasing the stick returns to Standing and stops movement | COMBAT_SPEC Standing | `WalkingTests.Walk_Release_ReturnsToStandingAndStops` | Auto |
| M1.6 | Keyboard WASD moves the player through `PlayerInputSource` | D-019, ARCHITECTURE 4.1 | `MovementSliceSceneTests.PlayerInput_Keyboard_WalksAndRuns` (test keyboard, CPU silenced) | Auto |
| M1.7 | Gamepad left stick and D-pad move the player | CONTROLS | none | **Human** |
| M1.8 | Walk speeds, depth speed and deadzone feel right | D-005 starting points | none | **Human** |

### M2. Arena bounds and obstacles (ARCHITECTURE 3.1, D-018)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M2.1 | Feet stay inside the walkable band on all four sides, minus the feet box. The real band is x 1.5–25.1667, y −13.6667 to −9.6667 (D-027 narrowed the ends) | 3.1 rect table, D-027 | Clamping rule: `WalkingTests.Walk_ClampsTo{Left,Right,Front}Edge`, `Walk_ClampsToBackWall`, `Walk_DiagonalIntoCorner_*` (on the movement-slice band fixture `SliceArena`, x 0–26.6667); band numbers: M2.7 | Auto |
| M2.2 | Truck footprint blocks walking from its reachable sides (right, front) | 3.1 | `ObstacleTests.Truck_BlocksFromTheRight`, `Truck_BlocksFromTheFront` | Auto |
| M2.3 | Pool footprint blocks walking from its reachable sides (left, front) | 3.1 | `ObstacleTests.Pool_BlocksFromTheLeft`, `Pool_BlocksFromTheFront` | Auto |
| M2.4 | Walking along or diagonally into a face slides without snagging, including at the corners; the feet never enter an obstacle | 3.1 | `ObstacleTests.*Slide*`, `*Diagonal*`, `Truck_DiagonalStraightAtCorner_*` (checked every tick) | Auto |
| M2.5 | Blocking still works with a non-zero feet depth | D-005 tuning | `ObstacleTests.Obstacles_WithFeetDepth_StillBlockAndSlide` | Auto |
| M2.6 | Pushing into a wall keeps the wrestler Walking in place | implementation | `WalkingTests.Walk_PushingIntoWall_StaysWalkingInPlace` | Auto, **Gap** |
| M2.7 | `Arena.prefab` markers produce exactly the 3.1 rects (one band at x 1.5–25.1667, truck, pool) | 3.1, D-027 | `ArenaPrefabTests.ArenaPrefab_MarkersBuildTheDocumentedRects`; also checked from the live scene in `MovementSliceSceneTests.Match_Builds*` | Auto |
| M2.8 | A huge `dt` is clamped to `maxTickStep`; even a raised clamp cannot tunnel through the truck or pool | D-021 Tick | `TickStepTests.*` | Auto |
| M2.9 | The truck and pool footprint depths match where the art reads as solid (feet don't overlap or float off the sprites) | 3.1 ("playtesting should confirm") | none | **Human** |
| M2.10 | Edge behavior looks right: wrestlers stop at the trailer, front edge and sides without looking cut off or stuck | 3.1 | none | **Human** |
| M2.11 | Depth sorting: a wrestler in front of or behind the truck or pool draws on the correct side of it | 3.1, 4.2 Depth sorting | none | **Human** |

### M3. Press-once running (D-020, COMBAT_SPEC Running, CONTROLS Running)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M3.1 | One Run press starts a run in the facing left/right direction; it continues with no input held | D-020 | `RunningTests.Run_OnePressFromStanding_*`, `Run_FromWalkingLeft_RunsLeft`, PlayMode `PlayerInput_Keyboard_WalksAndRuns` (Space tap) | Auto |
| M3.2 | Facing straight up or down, the run goes toward the last left/right facing | D-020 | `RunningTests.Run_FacingStraightUpOrDown_UsesLastHorizontalFacing` (4 cases) | Auto |
| M3.3 | A diagonal facing runs along its horizontal part | D-020 | `RunningTests.Run_FacingDiagonal_RunsHorizontally` | Auto |
| M3.4 | A run is left or right only: holding up or down does not steer depth | COMBAT_SPEC Running | `RunningTests.Run_HoldingUpOrDown_DoesNotSteerDepth` | Auto |
| M3.5 | The run ends at the arena ends and the back wall (back to Standing, no damage); a run into the truck or pool is Struck with obstacle collision damage, then Standing (D-027; Knocked Down below 30, D-030, see C9) | D-020, D-027 | `RunningTests.Run_StopsAt{Right,Left}Edge_*`, `Run_StopsAtTruck_StruckThenStanding`, `Run_StopsAtPool_StruckThenStanding`, `Run_StartedAgainstEdge_*`; `RunRulesTests.RunIntoArenaEndOrBackWall_NoDamageNoEvent` | Auto |
| M3.6 | After a run the wrestler can walk and run again | D-020 | `RunningTests.Run_AfterStopping_CanWalkAndRunAgain` | Auto |
| M3.7 | Run ends on "gets hit" or "performs another action" | D-020, D-029 | Combat-core slice C9.2, C9.3 (`StrikeTests.Strike_LandsOnRunningWrestler`, `RunRulesTests.StrikeWhileRunning_*`) | Auto |
| M3.8 | Recovering: walking slowed, running not allowed | COMBAT_SPEC Recovering | Combat-core slice C7 (`RecoveringTests.Recovering_WalksAtReducedSpeed_*`, `Recovering_CannotRun`) | Auto |
| M3.9 | Run speed feels right; a run stopping dead at the edge or an obstacle reads well | D-005 starting points | none | **Human** |
| M3.10 | Holding a direction and pressing Run on the same frame runs the way the player expects | CONTROLS ("direction player is facing") | none | **Human**, **Gap** (see report) |

### M4. Facing (ARCHITECTURE 4.2, STYLE_GUIDE animations)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M4.1 | `Facing` is the unit vector of the last 8-way direction | 4.2 | `FacingTests.Walk_SetsFacingToUnitEightWayDirection_*` (8 cases) | Auto |
| M4.2 | Back-facing = Up-Left, Up, Up-Right (`Facing.y > 0`); the other five are front-facing | STYLE_GUIDE Walking Back/Front Facing | same test, plus PlayMode `WrestlerView_BackFacingRule_AllEightDirections` | Auto |
| M4.3 | `HorizontalFacing` follows any left or right component and is kept when facing straight up or down | 4.2, D-020 | `FacingTests.Walk_DiagonalSetsHorizontalFacing`, `Walk_StraightUpOrDown_KeepsLastHorizontalFacing` | Auto |
| M4.4 | Standing keeps the last facing ("stand to match the most recent animation") | STYLE_GUIDE Standing | `FacingTests.Standing_KeepsLastFacing` (8 cases) | Auto |
| M4.5 | At match start the wrestlers face each other | implementation | `FacingTests.MatchStart_WrestlersFaceEachOther`; PlayMode flip check in `Match_Builds*` | Auto, **Gap** |
| M4.6 | `WrestlerView` flips Barry's left-facing art when `HorizontalFacing` = +1, sets Animator `State` = (int)CombatState and `Back` = 0/1, and never moves the root | 4.2 | `PresentationTests.WrestlerView_FlipsForRightFacing_AndSetsAnimatorState` | Auto |
| M4.7 | The sprite and animation are correct in all 8 walk directions and both standing facings (correct clip, correct flip, no flicker when changing direction) | STYLE_GUIDE | none | **Human** |

### M5. Combat events (ARCHITECTURE 4.2)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M5.1 | `StateChanged` fires once per real transition, with a correct from/to chain and wrestler id | 4.2 | `CombatEventsTests.StateChanged_*` | Auto |
| M5.2 | `FacingChanged` fires once per real facing change; not for the same direction, not for stick noise in one sector, not on Standing | 4.2 | `CombatEventsTests.FacingChanged_*` | Auto |

### M6. Shared rules, CPU stand-in, tuning (CLAUDE.md, ARCHITECTURE 4.1, D-005)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M6.1 | The CPU's intents go through the same rules as the player's | CLAUDE.md Unity Rules, 4.1 | `SharedRulesTests.CpuAndPlayer_SameIntents_SameResults` | Auto |
| M6.2 | ~~The idle stand-in CPU never moves~~ Replaced: Match.unity's CPU is driven by `OpponentBrain` (ARCHITECTURE 4.4). `IdleInput` is still the test harness's silent opponent | 4.4 | `SharedRulesTests.IdleInput_*`, `IdleCpu_*` (harness); PlayMode `MovementSliceSceneTests.Match_CpuIsDrivenByOpponentBrain_AndApproachesThePlayer` (replaces `Match_CpuStandInStaysIdle`) | Auto |
| M6.3 | Each input is read once per tick | 4.1 | `SharedRulesTests.Inputs_AreReadOncePerTick` | Auto |
| M6.4 | Tuning edits (walk and run speeds, deadzone, feet width, max step) apply on the next tick without rebuilding the match | D-005, ARCHITECTURE 5 | `SharedRulesTests.TuningEdit_*` | Auto |
| M6.5 | Editing `CombatTuning.asset` in the Inspector during Play Mode changes the running game | D-005 | none | **Human** |
| M6.6 | ~~Wrestlers pass through each other~~ Replaced by D-027: wrestlers block each other (see C10) | D-027 | `BlockingTests.*` | Auto |

### M7. Match scene assembly (ARCHITECTURE 4.4)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M7.1 | Loading `Match.unity` with no GameSession setup builds a match from roster entries 0 and 1 with no console errors | 4.4 | `MovementSliceSceneTests`, `CombatSceneTests` (any logged error fails the test) | Auto |
| M7.2 | The wrestlers spawn at the feet points (9.0, −11.6667) and (17.6667, −11.6667); the roots follow `IWrestlerView.Position` | 4.4 | `Match_BuildsTwoWrestlersAtSpawnPoints_AndBindsViewsAndCamera` (CPU silenced so the brain doesn't walk it off its spawn before the check) | Auto |
| M7.3 | Each `WrestlerView` is bound to its own wrestler, and `MatchBootstrap` adds `PlayerInputSource` | 4.4 | same test, plus `PlayerInput_Keyboard_WalksAndRuns` | Auto |
| M7.4 | Player input moves only the player | 4.1 | `PlayerInput_Keyboard_WalksAndRuns` (CPU silenced) | Auto |

"CPU silenced": the test's `MatchSceneLoader` calls the public `CombatMatchRunner.Configure(null, null, null, IdleInput.Instance)` in `SceneManager.sceneLoaded` (after Awake, before Start), so the runner reads `IdleInput` instead of the brain. No production change; `MatchBootstrap` still creates and exposes `CpuBrain`.

### M8. Match camera (D-017, ARCHITECTURE 3.1)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M8.1 | Default orthographic size is 4.5 at 16:9 (60% of the arena width); the maximum is 7.5 (whole arena) | D-017, 3.1 | `PresentationTests.MatchCamera_DefaultSize_ZoomsOutAndBackIn`; also in the live scene | Auto |
| M8.2 | The camera centers on the wrestlers' midpoint; the default view is centered on canvas row 570 | 3.1 | `MovementSliceSceneTests.Match_Builds*` | Auto |
| M8.3 | It zooms out as the wrestlers separate (size = (half separation + edgeMargin) / aspect), keeps both on screen, and caps at the whole arena | D-017 | `MatchCamera_DefaultSize_ZoomsOutAndBackIn` | Auto |
| M8.4 | It eases back to exactly the default size when they close in, and the position snaps to 1/60 unit at the default zoom | D-017, 3.1 | same test | Auto |
| M8.5 | The view never leaves the arena art | 3.1 | `MatchCamera_NeverShowsOutsideTheArena` | Auto |
| M8.6 | Zoom and follow feel smooth (no jitter, pumping or lag); pixels look crisp at the default zoom at 1920×1080 | D-017 | none | **Human** |

### Movement Slice: human playtest checklist

1. Walk all 8 directions with the keyboard (WASD), the left stick and the D-pad. Check the sprite flip and the front/back clip in each direction, and that stopping keeps the last facing (M1.7, M4.7).
2. Speeds: horizontal 3.0, depth 2.0, diagonal 3.6, run 7.0 u/s (D-029). Are these OK? (M1.4, M1.8, M3.9)
3. Deadzone 0.3: does a resting or worn stick drift? Do small pushes register when they should? (M1.8)
4. Truck and pool footprint depths against the art: walk along each front face and around each corner (M2.9).
5. Edges: trailer back wall, front edge, left and right sides. Does the wrestler stop in a believable place? (M2.10)
6. Depth sorting with the truck and pool (M2.11).
7. Runs: tap B or Space from each facing, including straight up or down. Does stopping dead at edges and obstacles feel right? Does holding a direction and pressing Run together go the expected way? (M3.9, M3.10)
8. Camera: the CPU now approaches, so run away from it in both directions (or knock it down first). Check zoom-out smoothness, the zoom-in return, and pixel crispness at the default zoom (M8.6).
9. Edit `CombatTuning.asset` during Play Mode and confirm the change applies at once (M6.5).

---

## Combat-Core Slice

Scope: Striking and the 1-2-3 combo, Struck, Damage, Knocked Down (with mashing and the Stomp), Recovering, Knocked Out and draws, the running and blocking rules from the movement playtest, the CPU brain, presentation of combat events, and Match.unity end to end (COMBAT_SPEC Striking/Combo Rules, Damage, Knocked Down, Tuning Values, CPU AI; D-024 to D-030; ARCHITECTURE 4.1–4.4, 5). Lock-ups, grapples and the Irish Whip are the next slice.

Test files: `Assets/Tests/EditMode/Combat/*.cs` (`StrikeTests`, `KnockdownTests`, `StompAndRecoveringTests` = `StompTests` + `RecoveringTests`, `RunAndBlockingTests` = `RunRulesTests` + `BlockingTests`, QA's `CombatCoverageTests`), `Assets/Tests/EditMode/AI/*.cs` (`OpponentBrainTests`, QA's `OpponentBrainCoverageTests`), `Assets/Tests/PlayMode/CombatPresentationTests.cs`, `Assets/Tests/PlayMode/CombatSceneTests.cs`.

Last run: 2026-10-31. EditMode 233/234 (1 known failure, C1.4). PlayMode 15/15.

### C1. Striking and the combo (COMBAT_SPEC Striking/Combo Rules, CONTROLS X, D-024, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C1.1 | One press = Strike 1; it lands when its wind-up ends, deals Strike 1 damage, defender Struck; phases Startup → Active → Recovery → None, total = tuned timing | Spec, D-024 | `StrikeTests.Strike1_LandsWhenWindUpEnds_DamagesAndStruck`, `Strike_PhasesRunStartupActiveRecoveryNone_ThenStanding`, `Strike_TotalLengthMatchesTiming` | Auto |
| C1.2 | Second and third presses inside the combo window give Strike 2 and Strike 3; Strike 3 knocks down; 3+4+6 damage | Spec | `StrikeTests.Combo_ThreePresses_Strike123_KnocksDown`, `Combo_PressAfterStrikeEnds_InsideWindow_ThrowsStrike2` | Auto |
| C1.3 | A press during a strike, inside the window, is buffered and continues the combo when the strike ends | Spec, D-030 | `StrikeTests.Combo_ThreePresses_*`; `ComboTimingTests.BufferedCombo_ThirdPressInsideWindowOfSecondPress_IsStrike3` | Auto |
| C1.4 | The window is measured from the previous strike's **press**, also for a buffered strike: a third press 0.55 s after a buffered second press is outside the 0.5 s window | D-030, CONTROLS | `ComboTimingTests.BufferedCombo_ThirdPressOutsideWindowOfSecondPress_IsNotStrike3` | Auto, **FAILS** (Combat restarts the window when the buffered strike starts, not at its press) |
| C1.5 | A press outside the window restarts at Strike 1; a press during a strike after the window closed does not continue the combo | Spec | `StrikeTests.Combo_PressOutsideWindow_StartsAtStrike1Again`; `ComboTimingTests.PressDuringStrike_AfterWindowClosed_DoesNotContinueCombo` | Auto, **Gap** (whether that late press should queue a Strike 1 is unspecified; today it is dropped) |
| C1.6 | There is no Strike 4: after Strike 3 the next press is Strike 1 | Spec | `ComboTimingTests.AfterStrike3_NextPressInsideWindow_IsStrike1` | Auto |
| C1.7 | Wrestlers can't move while Striking | D-024 | `StrikeTests.Striking_IsRooted` | Auto |
| C1.8 | The attacker may strike from Standing, Walking, Running (ends the run, Strike 1), Striking (combo), Recovering; not while Struck or Knocked Down | Spec allowed-state list, D-029 | `RunRulesTests.StrikeWhileRunning_*`, `ComboTimingTests.StrikeFromRun_InsideComboWindow_IsStrike1`, `RecoveringTests.Recovering_CanStrike_ReturnsToRecovering`, `StrikeAllowedStatesTests.Struck_CannotStrikeOrMove`, `KnockedDown_StrikeIsMashNotAMove_AndCannotMove` | Auto |
| C1.9 | A strike lands on a defender who is Standing, Walking, Running, Striking, Struck, Lock Up or Recovering; on Knocked Down only as a Stomp; never on Locked Up, Grappling, Grappled, Knocked Out | Spec allowed-state list, D-024, D-025 | `StrikeAllowedStatesTests.CanBeStruck_MatchesSpecList` (12 cases), `StrikeTests.Strike_LandsOnRunningWrestler`, `KnockdownTests.KnockedDown_ComboStrikeDoesNotLand_*`, `LockUpTests.Whiff_CanBeStruck`, `LockUp_PressedAsOpponentHitWindowOpens_Whiffs_AndTheStrikeLands` | Auto (Locked Up at the rule level only: nobody can strike a Locked Up wrestler while B has no control and A's Strike is ignored) |
| C1.10 | Reach and depth: hits only in front, within reach + hurt box and the depth tolerance; a strike hits at most once | Combat starting points | `StrikeTests.Strike_FacingAway_Whiffs`, `Strike_DepthTolerance`, `Strike1_Reach_*`, `Strike_HitsOnlyOnce` | Auto |

### C2. Strike trades (COMBAT_SPEC Striking, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C2.1 | When both strike, the first hit to land wins; the other strike is cancelled (never becomes active) and its thrower enters Struck | Spec | `StrikeTests.BothStrike_EarlierHitWindowWins_OtherCancelled`, `BothStrike_CpuFirst_CpuWins`, `StrikeAllowedStatesTests.FirstStrikeToLand_LoserIsCancelledAndStruck` | Auto |
| C2.2 | Hit windows opening on the same tick: both land | D-030 | `StrikeTests.BothStrike_SameTick_Trade` | Auto |

### C3. Struck (D-024)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C3.1 | Struck lasts `struckDuration`, then Standing (or Recovering) | D-024 | `StrikeTests.Struck_LastsStruckDuration_ThenStanding` | Auto |
| C3.2 | Each hit restarts Struck; no repeated `StateChanged` | D-024, ARCHITECTURE 4.2 | `StrikeTests.Struck_RestartedByNextComboHit_NoRepeatStateChanged` | Auto |
| C3.3 | A Struck wrestler can't strike or move | Spec (Struck is "Wrestler B only") | `StrikeAllowedStatesTests.Struck_CannotStrikeOrMove` | Auto |

### C4. Damage and knockdowns (COMBAT_SPEC Damage, Knocked Down, D-025, D-029)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C4.1 | Health starts at max; damage per move comes from tuning; health never goes below 0; `HealthChanged` on damage | Spec | `StrikeTests.Strike1_*`, `KnockdownTests.HealthNeverBelowZero_*`, `TuningEdit_Damage_*` | Auto |
| C4.2 | A strike that leaves health below 30 knocks down; the check is after the damage | Spec, D-029 | `KnockdownTests.Strike_KnockdownCheckIsAfterDamage` (32 → down, 34 → Struck), `RecoveringTests.Recovering_Struck_IsKnockedDown` | Auto |
| C4.3 | Knockdown length is linear in health lost, min at full health to max near 0 | D-025 | `KnockdownTests.KnockdownDuration_LinearInHealthLost`, `KnockdownDuration_Endpoints` | Auto |
| C4.4 | At the end of a knockdown: Standing at 30+ health, Recovering below | Spec | `KnockdownTests.Knockdown_EndsStanding_*`, `Knockdown_EndsRecovering_*` | Auto |
| C4.5 | A downed wrestler can't move or strike; no invulnerability after getting up | Spec, D-025 | `StrikeAllowedStatesTests.KnockedDown_StrikeIsMashNotAMove_AndCannotMove`, `KnockdownTests.KnockedDown_ComboStrikeDoesNotLand_ButLandsRightAfterGettingUp` | Auto |

### C5. Mashing (COMBAT_SPEC Knocked Down, Tuning Values, D-025)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C5.1 | Each Strike or Grapple press removes `mashReductionPerPress` (8%) of the remaining time, compounding; a held button is not a press | Spec | `KnockdownTests.Mash_StrikePress_*`, `Mash_GrapplePress_AlsoCounts`, `Mash_HeldButtonWithoutNewPress_DoesNothing`, `KnockdownMashAndStompTests.Mash_PressesCompoundOnRemainingTime` | Auto |
| C5.2 | Mashing never shortens a knockdown below **Shortest possible knockdown** (0.5 s), which is not Minimum knockdown time (1.0 s) | Spec | `KnockdownTests.Mash_NeverBelowShortestKnockdown`, `KnockdownMashAndStompTests.Mash_FullHealthKnockdown_EndsAtShortestKnockdown_NotMinimumKnockdownTime` | Auto |
| C5.3 | The CPU mashes at `cpuMashRate` through the same rule (one `Pressed` per press); rate 0 = no presses | D-025, ARCHITECTURE 4.1 | `OpponentBrainTests.Mashes_AtMashRate_*`, `MashRateZero_NoPresses` | Auto |

### C6. Stomp (D-025)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C6.1 | Strike over a Knocked Down opponent is a Stomp: damage only, knockdown timer unchanged, no new knockdown | D-025 | `StompTests.Strike_OverKnockedDownOpponent_IsStomp_DamageOnly_TimerUnchanged` | Auto |
| C6.2 | Out of stomp range it is a normal strike (which can't land on a downed wrestler); a stomp misses if the opponent gets up first | D-025 | `StompTests.Stomp_OutOfRange_IsComboStrikeInstead`, `Stomp_OpponentGetsUpDuringWindUp_Misses` | Auto |
| C6.3 | A stomp can knock out | D-025 | `StompTests.Stomp_CanKnockOut` | Auto |
| C6.4 | Cooldown 1.0 s from the end of a stomp; Strike during it over a downed opponent does nothing; no limit per knockdown | D-025, D-030 | `StompTests.Stomp_Cooldown_StartsWhenStompEnds`, `KnockdownMashAndStompTests.Stomp_NoLimitPerKnockdown` | Auto |

### C7. Recovering (COMBAT_SPEC Recovering, Damage, D-006, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C7.1 | Walks at the reduced speed and stays Recovering; can't run | Spec | `RecoveringTests.Recovering_WalksAtReducedSpeed_StaysRecovering`, `Recovering_CannotRun` | Auto |
| C7.2 | Regenerates `recoveryRate` per second only while standing still (no move input, even when blocked) | Spec, D-030 | `RecoveringTests.Recovering_RegeneratesOnlyWhileStandingStill`, `RecoveringStillnessTests.Recovering_PushingIntoOpponent_DoesNotRegenerate` | Auto |
| C7.3 | Regeneration stops at the recovery cap and the wrestler returns to Standing | D-006 | `RecoveringTests.Recovering_ReachesCap_BecomesStanding_StopsRegenerating` | Auto, **Gap** (a cap different from the threshold is undefined; see the QA report) |
| C7.4 | Can strike while Recovering; any hit while Recovering knocks down | Spec | `RecoveringTests.Recovering_CanStrike_*`, `Recovering_Struck_IsKnockedDown` | Auto |

### C8. Knockout, draw and the frozen match (COMBAT_SPEC Damage, ARCHITECTURE 4.2, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C8.1 | Health 0 = Knocked Out; `MatchEnded` once with winner, loser and elapsed time; `IsOver` and `Result` set | Spec, 4.2 | `KnockdownTests.KnockoutAtZero_RaisesMatchEndedOnce_AndFreezes`, `Knockout_PlayerLoses_CpuWins`, `SingleKnockout_IsNotDraw` | Auto |
| C8.2 | After the knockout nothing changes: `ElapsedSeconds` stops; positions, states and events freeze | 4.2 | `KnockdownTests.KnockoutAtZero_*` | Auto |
| C8.3 | Both at 0 on the same tick = draw (strike trade or run collision); `MatchEnded` once | D-030 | `KnockdownTests.DoubleKnockout_SameTick_IsDraw`, `DoubleKnockout_RunCollision_IsDraw` | Auto |
| C8.4 | ~1.5 s freeze, then the Victory screen (with a draw message for a draw) | D-026, D-030 | none (UI step) | Deferred |

### C9. Running rules (D-027, D-029, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C9.1 | Run again cancels your own run on the spot | D-027 | `RunRulesTests.RunAgain_CancelsOwnRun_StopsOnTheSpot` | Auto |
| C9.2 | Strike while running ends the run and throws Strike 1 on the spot (even inside a combo window); Lock Up does nothing | D-029 | `RunRulesTests.StrikeWhileRunning_EndsRun_ThrowsStrike1OnTheSpot`, `LockUpWhileRunning_DoesNothing`, `ComboTimingTests.StrikeFromRun_InsideComboWindow_IsStrike1` | Auto |
| C9.3 | A runner who is hit is Struck and the run does not resume | Spec | `StrikeTests.Strike_LandsOnRunningWrestler` | Auto |
| C9.4 | Truck or pool: `ObstacleHit`, obstacle collision damage, Struck; Knocked Down if below 30 after the damage; Knocked Out at 0. Arena ends and back wall: no damage, no event | D-027, D-030 | `RunRulesTests.RunIntoTruck_*`, `RunIntoPool_*`, `RunIntoArenaEndOrBackWall_NoDamageNoEvent` | Auto |
| C9.5 | Running into the opponent knocks both down (each by their own health), with run-collision damage (default 0, tunable) to both; head-on runs knock each down once | D-029, D-030 | `RunRulesTests.RunIntoOpponent_*`, `BothRunHeadOn_BothKnockedDownOnce` | Auto |
| C9.6 | A runner passes over a downed opponent; walking is still blocked by one; no collision at another depth | D-030 | `RunRulesTests.RunIntoKnockedDownOpponent_PassesOver_NoCollision`, `WalkIntoKnockedDownOpponent_StillBlocked`, `StopOnTopOfDownedOpponent_CanWalkOff`, `RunPastOpponentOnAnotherDepth_NoCollision` | Auto |

### C10. Blocking and walk speeds (D-027, D-029)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C10.1 | Feet can't overlap; a walk stops at contact with no pushing; feet never overlap in a scramble | D-027 | `BlockingTests.WalkIntoIdleOpponent_*`, `WalkInDepthIntoOpponent_*`, `Scramble_FeetNeverOverlap`, `DifferentDepth_WalkPast` | Auto |
| C10.2 | Resolution doesn't favor either wrestler | ARCHITECTURE 4.2 | `BlockingTests.BothWalkToward_MeetSymmetrically`, `RolesSwapped_SameOutcome` | Auto |
| C10.3 | Three walk speeds: horizontal, vertical, diagonal | D-029 | M1.3, M1.4 | Auto |

### C11. CPU opponent (COMBAT_SPEC CPU AI, ARCHITECTURE 4.1, D-025, D-030)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C11.1 | The CPU's intents go through the same rules as the player's: a recorded brain replayed through scripted input gives the same match; a combo by the CPU equals the same combo by the player | CLAUDE.md, 4.1 | `OpponentBrainTests.BrainIntents_ReplayedThroughScriptedInput_GiveSameMatch`, `SharedCombatRulesTests.Combo_PlayerOnCpu_EqualsCpuOnPlayer`, `SharedRulesTests.CpuAndPlayer_SameIntents_SameResults` | Auto |
| C11.2 | Approach, Attack (full combo), Stunned, Recover and Knockout states; stomps a downed player at its chance | Spec CPU AI | `OpponentBrainTests.ApproachesAndStrikesPlayer`, `ApproachesFromTheLeftToo_TurnsToFace`, `FullCombo_KnocksPlayerDown`, `StompsDownedPlayer_*`, `NeverStomps_*`, `Knockout_NoInput` | Auto |
| C11.3 | Decisions on an interval, not every frame; same seed = same match | Spec CPU AI | `OpponentBrainTests.ChosenStateChanges_AreSpacedByDecisionInterval`, `SameSeed_SameMatch` | Auto |
| C11.4 | While Recovering: stands still to recover, with a tunable chance (30%) to strike back at a player in range | D-030 | `OpponentBrainTests.Recovering_WithZeroCounterChance_*`, `OpponentBrainCoverageTests.Recovering_CounterChanceCertain_StrikesBackAtPlayerInRange`, `Recovering_CounterChanceZero_DoesNotStrikeBack` | Auto |
| C11.5 | In Match.unity the CPU is driven by the brain, approaches an idle player and lands Strike 1 | 4.4 | `MovementSliceSceneTests.Match_CpuIsDrivenByOpponentBrain_AndApproachesThePlayer`, `CombatSceneTests.CpuBrain_ApproachesIdlePlayer_AndLandsAStrike` | Auto |
| C11.6 | The CPU is a fair, beatable, varied opponent | GDD pillars | none | **Human** |

### C12. Tuning (COMBAT_SPEC Tuning Values, D-005, ARCHITECTURE 5)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C12.1 | A new `CombatTuning` carries every COMBAT_SPEC default (timing, damage, recovery, knockdown, walk speeds, stomp cooldown, CPU mash rate, D-030 counter chance) | Spec table | `TuningDefaultsTests.NewTuning_HasCombatSpecDefaults` | Auto |
| C12.2 | Values are read at use time, so edits after the match is built apply on the next use: strike timing and damage, Struck duration, combo window, threshold, knockdown times, mash share, recovery rate, obstacle and stomp damage, stomp cooldown | D-005 | `StrikeTests.TuningEdit_StrikeTiming_*`, `KnockdownTests.TuningEdit_Damage_*`, `TuningAtUseTimeTests.*` (8), `KnockdownMashAndStompTests.StompCooldown_TuningEditMidMatch_*`, `SharedRulesTests.TuningEdit_*`; in Play Mode, `CombatPresentationTests.Strike_MoveSpeedFromTable_*` (Startup edit changes the clip speed) | Auto |
| C12.3 | Editing `CombatTuning.asset` in the Inspector during Play Mode changes the running fight | D-005 | none | **Human** |

### C13. Presentation of combat (ARCHITECTURE 4.2, D-023, D-024)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C13.1 | On `MoveStarted` the strike clip plays from its start at speed = clip contact time (MoveAnimationTable) / tuned Startup, read at that moment; a buffered combo strike (no state change) still restarts its clip; speed returns to 1 after the move | 4.2 Attack timing | `CombatPresentationTests.Strike_MoveSpeedFromTable_AndBufferedComboRestartsTheClip` | Auto |
| C13.2 | Struck restarts its clip on every hit (`HitLanded`) | D-024, 4.2 | `CombatPresentationTests.Struck_ClipRestartsOnEveryHit` | Auto |
| C13.3 | Knocked Out while already down holds the lying pose; Knocked Out from standing plays the fall | D-023, 4.2 | `CombatPresentationTests.KnockoutWhileDown_HoldsLyingPose`, `KnockoutFromStanding_PlaysTheFall` | Auto |
| C13.4 | A right-facing (flipped) wrestler mirrors the clip's x offset and z rotation; the root never moves | 4.2 | `CombatPresentationTests.FlippedWrestler_MirrorsClipOffsetAndRotation` | Auto |
| C13.5 | The contact frame visibly lands when the hit registers; the placeholder Struck, Knocked Down, Stomp and Recovering clips read correctly | STYLE_GUIDE | none | **Human** |

### C14. Match.unity end to end

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| C14.1 | Keyboard J throws Strike 1 through `PlayerInputSource` and lands on an adjacent CPU for Strike 1 damage | CONTROLS, D-019 | `CombatSceneTests.PlayerKeyboard_J_ThrowsStrike1_AndLandsOnTheCpu` | Auto |
| C14.2 | The CPU lands a strike on an idle player | 4.4 | `CombatSceneTests.CpuBrain_ApproachesIdlePlayer_AndLandsAStrike` | Auto |
| C14.3 | Banner for ~2 s with both wrestlers still; control and timer start when it ends | D-026 | none (UI step; Combat has no start gate yet, see the QA report) | Deferred |
| C14.4 | Gamepad X, A and B work like J, K and Space | CONTROLS | none | **Human** |

### Combat-Core Slice: human playtest checklist

1. Strikes with the keyboard (J) and gamepad (X): tap once, twice, three times. Does the 1-2-3 combo connect reliably, both when mashing and when pressing after each strike? Does a slow press drop back to Strike 1 when you expect it to? (C1, C14.4)
2. Buffered combo timing: press twice quickly, then wait a little before the third press. Today the third press is accepted up to 0.5 s after Strike 2 *starts* (the C1.4 failure). Which feels right: that, or 0.5 s from your second press, as D-030 says?
3. Strike reach and depth: do punches and the kick connect when they look like they should, and whiff when they look like they miss? Try slightly different depths. (C1.10)
4. Strike trades: both wrestlers swinging at once. Is it clear who won? (C2)
5. Struck: does the 0.4 s stagger read well, and does it visibly restart on each combo hit? (C3, C13.2)
6. Knockdowns: low health gives noticeably longer knockdowns; mashing J/K visibly speeds getting up; the shortest knockdown (0.5 s) still feels like a knockdown. (C4.3, C5)
7. Stomp: stand over a downed CPU and press Strike. Is the stomp range (2 units) right? Does the 1.0 s cooldown feel right? (C6)
8. Recovering: below 30 health the walk is half speed, Run does nothing, and standing still heals slowly to 30. Are the slow walk and 1 health/s OK? (C7)
9. Knockout: the loser stays down, the fight freezes and the timer stops. A stomp KO holds the lying pose without replaying the fall. (C8, C13.3)
10. Runs: cancel with Space/B; Strike from a run; run into the truck or pool (Struck, damage, knockdown at low health); run into the CPU (both go down); run over a downed CPU. (C9)
11. Blocking: wrestlers can't walk through each other; walking into the CPU never pushes it; no snagging when walking around it. (C10)
12. CPU: does it approach, combo, stomp, mash and sometimes strike back while Recovering? Is it too easy, too hard or repetitive? Does it stomp too often? (C11.6)
13. Flipped art: when facing right, falls and knock-backs go the right way. (C13.4)
14. Edit `CombatTuning.asset` during Play Mode (damage, combo window, Struck duration) and confirm the change applies at once. (C12.3)
15. Strike 1's extended arm near the arena ends can pass the art edge by about 26 px (ARCHITECTURE 3.1). Is that acceptable?

---

## Lock-Up Slice

Step 1: the standing lock-up, the whiff and the break (COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules, CONTROLS A button, D-028, D-031, ARCHITECTURE 6). Bodyslam (step 2) and Irish Whip (step 3) add their own sections. Tests: `Assets/Tests/EditMode/Combat/LockUpTests.cs`, `Assets/Tests/EditMode/AI/OpponentBrainLockUpTests.cs`.

### L1. Lock-up success (COMBAT_SPEC, D-028, ARCHITECTURE 6)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L1.1 | Lock Up from Standing, Walking or Recovering on an opponent who is Standing, Walking or Recovering, in reach and in front: A = Lock Up, B = Locked Up | Spec allowed list, D-028 | `LockUpTests.LockUp_FromStanding_OnStandingOpponent_*`, `LockUp_FromWalking_OnWalkingOpponent`, `LockUp_OnRecoveringOpponent`, `LockUp_FromRecovering`, `LockUp_PlayerAndCpuRoles_Swapped_SameRules` | Auto |
| L1.2 | Both snapped to the pair anchor (A stays, B `lockUpPairSpacing` in front at A's depth; A placed behind B if B's spot is off the arena), facing each other; `PairedMoveStarted` carries initiator, receiver, LockUp, anchor, facing and the timing (Active = lock-up timer) | ARCHITECTURE 6 | `LockUpTests.LockUp_FromStanding_OnStandingOpponent_SnapsAndRaisesPairedMoveStarted`, `Snap_ReceiverSpotOutsideArena_InitiatorIsPlacedInstead` | Auto |
| L1.3 | A strike still winding up (before its hit window opens) is cancelled by the lock-up and never lands; once its hit window is open the lock-up whiffs and the strike lands | Spec Striking (Wrestler B only) | `LockUpTests.LockUp_OnOpponentStrikeWindingUp_CancelsTheStrike`, `LockUp_SameTickOpponentStartsStrike_CancelsTheStrike`, `LockUp_PressedAsOpponentHitWindowOpens_Whiffs_AndTheStrikeLands` | Auto |
| L1.4 | Not on an opponent who is Running, Struck, Lock Up (whiffing), Knocked Down or Knocked Out (whiff instead); not out of reach, beyond the depth tolerance or facing away; Lock Up does nothing while Running, Striking or Struck | Spec, D-029 | `LockUpTests.CanBeLockedUp_MatchesSpecList` (17 cases), `LockUp_OnRunningOpponent_Whiffs`, `LockUp_OnStruckOpponent_Whiffs`, `LockUp_OnKnockedDownOpponent_Whiffs`, `LockUp_OnWhiffingOpponent_Whiffs`, `LockUp_OutOfReach_Whiffs`, `LockUp_BeyondDepthTolerance_Whiffs`, `LockUp_FacingAway_Whiffs`, `LockUp_WhileStruck_DoesNothing`, `LockUp_WhileStriking_DoesNothing`, `RunRulesTests.LockUpWhileRunning_DoesNothing` | Auto |
| L1.5 | Both press Lock Up in reach on the same tick: 50/50 winner from the injected random source; same seed, same winner; if only one reaches, that one wins | D-028 | `LockUpTests.Simultaneous_InReach_WinnerFromRandomSource` (2), `Simultaneous_SeededRandom_IsFiftyFifty_AndRepeatable`, `Simultaneous_OnlyOneReaches_ThatOneWins_NoRandom` | Auto |

### L2. Whiff (D-031)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L2.1 | No valid target in reach: Lock Up state for the whiff time (0.4 s, read at use), rooted, `MoveStarted(LockUp)` and no `PairedMoveStarted`, then Standing (or Recovering at low health) | D-031 | `LockUpTests.Whiff_LastsWhiffTime_Rooted_ThenStanding`, `Whiff_PositionUnchangedThroughout`, `Whiff_ReturnsToRecovering_WhenLowHealth`, `Whiff_TimeIsReadAtUse` | Auto |
| L2.2 | A whiffing wrestler can be struck; the whiff is cut short | D-031, C1.9 | `LockUpTests.Whiff_CanBeStruck` | Auto |

### L3. During the lock-up and the break (D-031, COMBAT_SPEC)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L3.1 | Wrestler B has no control: no moves, no movement, no strikes | D-031 | `LockUpTests.DuringLockUp_LockedUpWrestler_HasNoControl` | Auto |
| L3.2 | Wrestler A's Strike and movement are ignored | D-031 | `LockUpTests.DuringLockUp_InitiatorStrikeAndMovement_AreIgnored` | Auto |
| L3.3 | A's Irish Whip press does nothing yet (step 3); the timer still runs. (Step 1 also covered Grapple; since step 2 Grapple starts the bodyslam, see L5.) | Slice plan | `LockUpTests.DuringLockUp_InitiatorWhipPresses_DoNothingYet` | Auto |
| L3.4 | Timer (2.0 s, read at use) runs out: `PairedMoveStarted(BreakLockUp)`; when the break's timing ends both return to Standing or Recovering by health and have normal control | Spec | `LockUpTests.Timer_Expires_BreakLockUp_ThenBothStanding`, `Break_ReturnsLowHealthWrestlerToRecovering`, `AfterBreak_BothHaveNormalControl`, `LockUpTimer_IsReadAtUse` | Auto |
| L3.5 | New tuning carries lock-up timer 2.0 s and whiff time 0.4 s | Spec table | `LockUpTests.NewTuning_HasLockUpDefaults` | Auto |

### L4. CPU (COMBAT_SPEC CPU AI Lock Up/Grapple)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L4.1 | When it would strike a lockable player, the CPU goes for a lock-up at `cpuLockUpChance` (stepping into lock-up reach first if needed, never whiffing on purpose); at 0 it never does | Spec CPU AI | `OpponentBrainLockUpTests.LockUpChanceCertain_ApproachesAndLocksUpThePlayer`, `LockUpChanceZero_NeverLocksUp_Strikes` | Auto |
| L4.2 | In Lock Up with `cpuBodyslamChance` 0 the CPU holds (no input) until the break, then decides again; Locked Up gives no input. (Step 2 adds the bodyslam choice, L6.) | Spec, D-031 | `OpponentBrainLockUpTests.InLockUp_CpuHoldsUntilTheBreak_ThenActsAgain`, `LockedUpCpu_GivesNoInput_UntilTheBreak` | Auto |
| L4.3 | Same seed, same choices | Spec CPU AI | `OpponentBrainLockUpTests.LockUpChoice_IsRepeatableWithTheSameSeed` | Auto |

### Lock-Up Slice step 1: human playtest checklist

Needs the presentation stand-ins (Lock Up, Locked Up, whiff and Break clips) before it can be judged visually.

1. Press K / A next to the CPU: does the lock-up range (feet about 1.7 apart or closer, same depth within 0.4) feel right? Too grabby or too fussy? (L1)
2. The snap: does the CPU visibly jump to 1.2 units in front of you? Is that spacing right for the art? (L1.2)
3. Lock up the CPU while it winds up a punch: is it clear the lock-up beat the punch? Try it a fraction later: the punch should win. (L1.3)
4. Whiff with nobody near: does 0.4 s of grabbing at air read as a mistake and feel punishable without being sluggish? (L2)
5. The 2.0 s hold with nothing to do (step 1): is the break (0.4 s) readable? (L3.4)
6. The CPU: how often does it go for a lock-up (25% of its strike choices)? Does stepping in for one look natural? (L4)
7. Both press K / A at once: does the coin flip feel fair? (L1.5)

## Lock-Up Slice step 2: Bodyslam

Grapple (A / K) during a lock-up performs a bodyslam (COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules "Wrestler A presses the Grapple button", Grappling / Grappled states, Damage table, Knocked Down; ARCHITECTURE 6; D-033 art and timing). Tests: `Assets/Tests/EditMode/Combat/BodyslamTests.cs`, `Assets/Tests/EditMode/AI/OpponentBrainBodyslamTests.cs`.

Timeline (default tuning): Grapple press → A Grappling, B Grappled, `CurrentMove` Bodyslam, `PairedMoveStarted(Bodyslam)` → impact at the initiator's bodyslam Startup (Barry 1.208 s, Peaux 1.083 s, D-033; 1.15 s when the match has no wrestler definitions): 12 damage, `HitLanded`, `HealthChanged`, B still Grappled → 1.21 s later (Active 0.1 + Recovery 1.11, the rest of both clips) B Knocked Down (length from health), A Standing or Recovering. A knockout at the impact ends the match at once.

### L5. The bodyslam (COMBAT_SPEC, ARCHITECTURE 6, D-033)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L5.1 | Grapple pressed by A anywhere in the lock-up window (including the first tick after the lock-up and late in the window): A = Grappling, B = Grappled, `CurrentMove` = Bodyslam for both, `PairedMoveStarted(Bodyslam)` with initiator, receiver, facing, anchor and the bodyslam timing; no `MoveStarted`; no damage yet | Spec, ARCHITECTURE 6 | `BodyslamTests.GrappleInLockUp_StartsBodyslam_GrapplingAndGrappled_RaisesPairedMoveStarted`, `GrapplePressedLateInTheLockUpWindow_StillStartsBodyslam`, `CpuInitiator_SameRules` | Auto |
| L5.2 | Neither root moves at the start, during, or at the end of the bodyslam (the art assumes both stay `lockUpPairSpacing` apart) | D-033, ARCHITECTURE 6 | `BodyslamTests.GrappleInLockUp_StartsBodyslam_*`, `MoveEnd_ReceiverKnockedDown_InitiatorStanding_RootsNeverMove` | Auto |
| L5.3 | Impact at the bodyslam Startup: Bodyslam damage (12) to B, `HitLanded(Bodyslam)` and `HealthChanged`; B stays Grappled (still on the clip) | Spec Damage table | `BodyslamTests.Impact_AtBodyslamStartup_AppliesDamage_ReceiverStaysGrappled` | Auto |
| L5.4 | At the end of the move (Startup + Active + Recovery): B Knocked Down with the normal health-based length (`KnockedDown` event), A Standing (or Recovering), both `CurrentMove` None; phase events Bodyslam Active → Recovery → None | Spec Grapple, Knocked Down, D-025 | `BodyslamTests.MoveEnd_ReceiverKnockedDown_InitiatorStanding_RootsNeverMove`, `Bodyslam_LowHealthReceiver_KnockedDownThenRecovering` | Auto |
| L5.5 | Bodyslam damage taking B to 0: Knocked Out at the impact, `MatchEnded` with A the winner | Spec Damage | `BodyslamTests.Bodyslam_ToZeroHealth_KnocksOutAtImpact_EndsTheMatch` | Auto |
| L5.6 | Neither wrestler has control during the bodyslam (no moves, movement, facing changes, second paired move) | Spec, D-031 | `BodyslamTests.DuringBodyslam_NeitherWrestlerHasControl` | Auto |
| L5.7 | After it, A has normal control (Strike over the downed B is a Stomp) | D-025 | `BodyslamTests.AfterBodyslam_InitiatorHasControl_AndCanStompTheDownedReceiver` | Auto |
| L5.8 | No bodyslam outside a lock-up: Grapple in range from Standing is a lock-up, out of range a whiff (presses during the whiff do nothing), Locked Up B's Grapple does nothing, Grapple during the break does nothing | Spec "A Grapple can only be performed when A is in Lock Up and B in Locked Up" | `BodyslamTests.GrapplePressInRange_OutsideALockUp_IsALockUp_NotABodyslam`, `GrapplePressOutOfRange_Whiffs_NoBodyslam`, `LockedUpWrestlerPressingGrapple_DoesNotBodyslam`, `GrappleDuringBreak_DoesNotBodyslam` | Auto |
| L5.9 | Bodyslam Startup comes from the initiator's `bodyslamStartupByInitiator` entry (Barry 1.208, Peaux 1.083 in the project asset), else `bodyslamTiming`; Active and Recovery are shared | D-033 | `BodyslamTests.PerInitiatorStartup_FromTheInitiatorsDefinition` | Auto (the project asset's entries are set; the Match scene passes the definitions only once MatchBootstrap does, see ARCHITECTURE 6) |
| L5.10 | Bodyslam damage and timing are read at use; new tuning carries damage 12 and Active + Recovery ≈ 1.209 s | Spec table, D-005 | `BodyslamTests.BodyslamDamageAndTiming_AreReadAtUse`, `NewTuning_HasBodyslamDefaults` | Auto |
| L5.11 | Strikes can't land on Grappling or Grappled | Spec allowed-state list | `StrikeAllowedStatesTests.CanBeStruck_MatchesSpecList` | Auto |

### L6. CPU bodyslam (COMBAT_SPEC CPU AI Lock Up/Grapple, demo steps 5-7)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| L6.1 | Holding a lock-up it started, the CPU presses Grapple at `cpuBodyslamChance` per decision tick, the first roll one decision interval into the lock-up; the player is knocked down with bodyslam damage | Spec CPU AI, demo step 6 | `OpponentBrainBodyslamTests.BodyslamChanceCertain_LocksUp_ThenBodyslams_OneIntervalIn` | Auto |
| L6.2 | At chance 0 the CPU holds until the break | Spec CPU AI | `OpponentBrainBodyslamTests.BodyslamChanceZero_HoldsTheLockUpUntilTheBreak` | Auto |
| L6.3 | No CPU input while Grappling; afterwards back to Idle / Evaluate | Demo step 7 | `OpponentBrainBodyslamTests.DuringItsBodyslam_CpuGivesNoInput_ThenReturnsToIdleEvaluate` | Auto |
| L6.4 | A Grappled CPU gives no input, then mashes once Knocked Down | D-031, D-025 | `OpponentBrainBodyslamTests.GrappledCpu_GivesNoInput_ThenIsKnockedDownAndMashes` | Auto |
| L6.5 | Same seed, same choices | Spec CPU AI | `OpponentBrainBodyslamTests.BodyslamChoice_IsRepeatableWithTheSameSeed` | Auto |

### Lock-Up Slice step 2: human playtest checklist

1. Lock up, then press K / A: does the slam start right away and feel responsive? Can you press too early (on the lock-up's first frame) and does that look abrupt? (L5.1)
2. Barry slams Peaux and Peaux slams Barry: does each clip play at its natural speed, with the health bar dropping exactly as the receiver hits the ground? (L5.3, L5.9; needs MatchBootstrap to pass the wrestler definitions, otherwise both use 1.15 s and play about 5% off)
3. Does the Knocked Down clip pick up cleanly where the Bodyslammed clip ends, or is there a jump? The receiver's root stays put while its lying body is drawn 1.1-1.5 units away from it; does the Knocked Down stand-in line up with that? (L5.2, L5.4)
4. Is 12 damage plus a knockdown the right reward compared with a 13-damage strike combo? (L5.3)
5. The CPU: with a 50% roll every 0.25 s it almost always slams within about half a second of locking up. Too predictable? Too punishing? (L6.1)
6. Bodyslam to knock out (low health): does the freeze on the impact frame read as a finish? (L5.5)
