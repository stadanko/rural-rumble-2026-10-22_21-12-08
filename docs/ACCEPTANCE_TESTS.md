# Rural Rumble Acceptance Tests

Completion criteria for each feature, written from the design documents (GDD, COMBAT_SPEC, CONTROLS, STYLE_GUIDE, ARCHITECTURE, DECISION_LOG). Owned by QA & Testing.

- Tests follow the spec, not the code. When they disagree, the test keeps the spec and QA reports the mismatch.
- Thresholds, speeds and timings are read from `CombatTuning` in tests, never hard-coded. The exceptions are the layout numbers ARCHITECTURE states (arena rects, spawn points, camera sizes).
- Passing tests do not prove the game feels right. Every feature lists the human playtest checks it still needs.

How to run (Editor open, `unity status` = ready):

```
unity command test run --mode editor --project-path "C:\Users\theke\Rural Rumble"
unity command test run --mode playmode --async_tests true --project-path "C:\Users\theke\Rural Rumble"
unity command test status --project-path "C:\Users\theke\Rural Rumble"
```

Run PlayMode with `--async_tests true` and poll `test status`. A synchronous `--mode PlayMode` call reported 0 tests.

Legend: **Auto** = covered by an automated test. **Human** = needs playtesting. **Gap** = the spec doesn't define it, so the test checks the current behavior and the question goes to the designer.

---

## Movement Slice

Scope: Standing, Walking and Running only (COMBAT_SPEC State Definitions; D-001, D-020), the walkable band and obstacles (ARCHITECTURE 3.1, D-018), the match camera (D-017), and Match.unity assembly (ARCHITECTURE 4.4, D-022).

Test files: `Assets/Tests/EditMode/Movement/*.cs` (helpers in `Assets/Tests/EditMode/Support/`) and `Assets/Tests/PlayMode/*.cs`.

Last run: 2026-10-30. EditMode 111/111 passed, PlayMode 8/8 passed.

### M1. Eight-way walking (D-001, CONTROLS.md)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M1.1 | The stick maps to exactly 8 directions; each 45° sector snaps to its nearest direction | D-001, CONTROLS | `MoveDirectionTests.Quantize_FullStick_*`, `Quantize_AngledStick_*` | Auto |
| M1.2 | Stick inside the deadzone (`moveDeadzone`, radial) = no direction; the wrestler stays Standing | ARCHITECTURE 4.1, 5 | `MoveDirectionTests.Quantize_InsideDeadzone_*`, `Quantize_JustOutsideDeadzone_*`, `Wrestler_StickInsideDeadzone_StaysStanding` | Auto |
| M1.3 | Horizontal walking moves at `walkSpeedHorizontal`, depth at `walkSpeedDepth` | ARCHITECTURE 5 | `WalkingTests.Walk_Horizontal_*`, `Walk_Depth_*` | Auto |
| M1.4 | Diagonals move each axis at its own tuned speed | implementation | `WalkingTests.Walk_Diagonal_EachAxisUsesItsTunedSpeed` | Auto, **Gap** (diagonals are about 20% faster than straight lines) |
| M1.5 | Releasing the stick returns to Standing and stops movement | COMBAT_SPEC Standing | `WalkingTests.Walk_Release_ReturnsToStandingAndStops` | Auto |
| M1.6 | Keyboard WASD moves the player through `PlayerInputSource` | D-019, ARCHITECTURE 4.1 | `MovementSliceSceneTests.PlayerInput_Keyboard_WalksAndRuns` (test keyboard) | Auto |
| M1.7 | Gamepad left stick and D-pad move the player | CONTROLS | none | **Human** |
| M1.8 | Walk speeds, depth speed and deadzone feel right | D-005 starting points | none | **Human** |

### M2. Arena bounds and obstacles (ARCHITECTURE 3.1, D-018)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M2.1 | Feet stay inside the walkable band on all four sides (x 0–26.6667, y −13.6667 to −9.6667, minus the feet box) | 3.1 rect table | `WalkingTests.Walk_ClampsTo{Left,Right,Front}Edge`, `Walk_ClampsToBackWall`, `Walk_DiagonalIntoCorner_*` | Auto |
| M2.2 | Truck footprint blocks walking from its reachable sides (right, front) | 3.1 | `ObstacleTests.Truck_BlocksFromTheRight`, `Truck_BlocksFromTheFront` | Auto |
| M2.3 | Pool footprint blocks walking from its reachable sides (left, front) | 3.1 | `ObstacleTests.Pool_BlocksFromTheLeft`, `Pool_BlocksFromTheFront` | Auto |
| M2.4 | Walking along or diagonally into a face slides without snagging, including at the corners; the feet never enter an obstacle | 3.1 | `ObstacleTests.*Slide*`, `*Diagonal*`, `Truck_DiagonalStraightAtCorner_*` (checked every tick) | Auto |
| M2.5 | Blocking still works with a non-zero feet depth | D-005 tuning | `ObstacleTests.Obstacles_WithFeetDepth_StillBlockAndSlide` | Auto |
| M2.6 | Pushing into a wall keeps the wrestler Walking in place | implementation | `WalkingTests.Walk_PushingIntoWall_StaysWalkingInPlace` | Auto, **Gap** |
| M2.7 | `Arena.prefab` markers produce exactly the 3.1 rects (one band, truck, pool) | 3.1 | `ArenaPrefabTests.ArenaPrefab_MarkersBuildTheDocumentedRects`; also checked from the live scene in `MovementSliceSceneTests.Match_Builds*` | Auto |
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
| M3.5 | The run stops at the arena edge and at the truck or pool, and the wrestler returns to Standing | D-020 | `RunningTests.Run_StopsAt{Right,Left}Edge_*`, `Run_StopsAtTruck_*`, `Run_StopsAtPool_*`, `Run_StartedAgainstEdge_*` | Auto |
| M3.6 | After a run the wrestler can walk and run again | D-020 | `RunningTests.Run_AfterStopping_CanWalkAndRunAgain` | Auto |
| M3.7 | Run ends on "gets hit" or "performs another action" | D-020 | not testable yet (no strikes or lock-ups in this slice) | Deferred |
| M3.8 | Recovering: walking slowed, running not allowed | COMBAT_SPEC Recovering | not in this slice | Deferred |
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
| M6.2 | The idle stand-in CPU never moves, changes state or changes facing | movement slice | `SharedRulesTests.IdleInput_*`, `IdleCpu_*`; PlayMode `Match_CpuStandInStaysIdle` | Auto |
| M6.3 | Each input is read once per tick | 4.1 | `SharedRulesTests.Inputs_AreReadOncePerTick` | Auto |
| M6.4 | Tuning edits (walk and run speeds, deadzone, feet width, max step) apply on the next tick without rebuilding the match | D-005, ARCHITECTURE 5 | `SharedRulesTests.TuningEdit_*` | Auto |
| M6.5 | Editing `CombatTuning.asset` in the Inspector during Play Mode changes the running game | D-005 | none | **Human** |
| M6.6 | Wrestlers pass through each other | implementation | indirectly in `IdleCpu_*` | **Gap** |

### M7. Match scene assembly (ARCHITECTURE 4.4)

| # | Criterion | Source | Test(s) | Type |
|---|---|---|---|---|
| M7.1 | Loading `Match.unity` with no GameSession setup builds a match from roster entries 0 and 1 with no console errors | 4.4 | `MovementSliceSceneTests` (all three; any logged error fails the test) | Auto |
| M7.2 | The wrestlers spawn at the feet points (9.0, −11.6667) and (17.6667, −11.6667); the roots follow `IWrestlerView.Position` | 4.4 | `Match_BuildsTwoWrestlersAtSpawnPoints_AndBindsViewsAndCamera` | Auto |
| M7.3 | Each `WrestlerView` is bound to its own wrestler, and `MatchBootstrap` adds `PlayerInputSource` | 4.4 | same test, plus `PlayerInput_Keyboard_WalksAndRuns` | Auto |
| M7.4 | Player input moves only the player | 4.1 | `PlayerInput_Keyboard_WalksAndRuns` | Auto |

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
2. Speeds: horizontal 3.0, depth 2.0, run 7.0 u/s. Diagonals are faster than straight lines. Are these OK? (M1.4, M1.8, M3.9)
3. Deadzone 0.3: does a resting or worn stick drift? Do small pushes register when they should? (M1.8)
4. Truck and pool footprint depths against the art: walk along each front face and around each corner (M2.9).
5. Edges: trailer back wall, front edge, left and right sides. Does the wrestler stop in a believable place? (M2.10)
6. Depth sorting with the truck and pool (M2.11).
7. Runs: tap B or Space from each facing, including straight up or down. Does stopping dead at edges and obstacles feel right? Does holding a direction and pressing Run together go the expected way? (M3.9, M3.10)
8. Camera: walk the player far from the idle CPU in both directions. Check zoom-out smoothness, the zoom-in return, and pixel crispness at the default zoom (M8.6).
9. Edit `CombatTuning.asset` during Play Mode and confirm the change applies at once (M6.5).
