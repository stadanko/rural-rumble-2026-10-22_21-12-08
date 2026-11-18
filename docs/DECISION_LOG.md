# Rural Rumble Decision Log

Accepted design decisions. When a decision here changes a requirement, the affected documents are updated to match, and this log records why.

Agents: read this before starting work. If a task conflicts with a decision here, stop and flag it rather than overriding it. Add new entries only for decisions the designer has approved.

| Field | Meaning |
|---|---|
| Decided by | Who approved it. Agent proposals are not decisions until the designer approves them. |
| Affects | Documents or systems that were changed or must follow the decision. |

---

## D-001: Movement is eight-directional with two-directional running

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Wrestlers walk in eight directions and run in two (left or right). The GDD's definition is authoritative.
- **Why:** CLAUDE.md said "four-directional movement", which conflicted with the GDD, CONTROLS.md, and COMBAT_SPEC.md.
- **Affects:** CLAUDE.md (MVP Scope corrected).

## D-002: Keyboard controls

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Keyboard mapping: Move = WASD, Lock Up/Grapple (A) = Left Shift, Run/Whip (B) = Space, Strike (X) = Right Shift.
- **Why:** CLAUDE.md requires keyboard input, but CONTROLS.md only mapped the controller.
- **Affects:** docs/CONTROLS.md.
- **Superseded:** the Shift bindings are replaced by D-019.

## D-003: No game mode select in the MVP

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Screen flow is Start Screen → Character Select → Match → Victory Screen. There is no Single Match / game mode selection screen in the MVP. The Victory Screen covers both outcomes ("Congratulations!" or "Game Over!").
- **Why:** The GDD listed a Single Match selection step that STYLE_GUIDE.md and CLAUDE.md did not.
- **Affects:** CLAUDE.md (MVP Scope), docs/GDD.md (Core Gameplay, MVP Scope).

## D-004: Thresholds are measured in health, not damage

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** State thresholds refer to a wrestler's remaining health. "Damage" means only the points an attack removes. The unspecified "certain threshold" for knockdowns is the Recovering threshold (30 health), matching the Knocked Down conditions.
- **Why:** COMBAT_SPEC.md said "below 30 points of damage" and "if damage is below a certain threshold" where it meant health.
- **Affects:** docs/COMBAT_SPEC.md.

## D-005: Combat tuning values are editable in the Unity editor

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** All combat tuning values live in one shared tuning asset, editable in the Inspector during Play Mode, so they can be tuned without code changes. Combo window = 0.5 s and lock-up timer = 2.0 s are set by the designer. Damage, recovery, and knockdown values are starting points modeled on classic arcade wrestling games, expected to change during playtesting.
- **Why:** COMBAT_SPEC.md asked for adjustable values but gave no defaults, which would have forced agents to invent them.
- **Affects:** docs/COMBAT_SPEC.md (Tuning Values section holds the current defaults).

## D-006: Recovery is capped

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Health recovered while standing still in Recovering stops at the recovery cap (default 30). The cap is its own tunable value, separate from the Recovering threshold.
- **Why:** Without a cap, a wrestler could stand still back to full health, undercutting the momentum-swing design pillar.
- **Affects:** docs/COMBAT_SPEC.md (Tuning Values).

## D-007: Initial agent crew

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Build the five core agents first: Systems Architect & Integration, Combat State & AI, Unity Animation & Tooling, UI & Match Flow, and QA & Testing. Art Wrangler, Sound Wrangler, Juice Agent, and Provenance Clerk are added later.
- **Why:** These five cover the code needed for a playable match. Asset and polish agents depend on that foundation.
- **Affects:** .claude/agents/.

## D-008: Art sourcing policy

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Art may come from OpenGameArt.org, itch.io free assets, Kenney.nl, and Craftpix's free section. Accepted licenses: CC0 and CC-BY. When nothing suitable is found, AI-generated art is allowed as a marked placeholder that the designer must review. Ripped commercial game art is never allowed. The Art Wrangler is created now (closes Q-003).
- **Why:** CLAUDE.md requires "permitted sources" but no document listed them.
- **Affects:** .claude/agents/art-wrangler.md, docs/ASSET_REGISTRY.csv (created with its column set).
- **Open question:** See Q-005 (Craftpix license).

## D-009: Art staging and folder layout

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Approves the two Architect proposals: unverified assets go in `Staging/` at the project root, outside `Assets/`; UI sprites live in `Assets/Art/UI/` (Art Wrangler adds files, UI & Match Flow owns import settings).
- **Why:** Keeps unverified art out of Unity imports and gives all art one drop location and provenance trail.
- **Affects:** docs/ARCHITECTURE.md (folder ownership).

## D-010: Custom wrestler sprites generated from concept art

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** The Art Wrangler may generate wrestler sprites with Unity's AI asset generation, using the designer's concept art (style-guide-01/02) as reference images. The concept art is mixed origin: designer hand sketches finished with ChatGPT. The WrestleFest sprites are used only as a written spec for poses, frame counts, and timing, never as generator input. Generated art contains no real brand logos (Barry's boots are generic work boots, no Carhartt logo). Generation works in `Assets/Art/_Generated/`, which nothing in the game references; approved art moves to its real `Assets/Art/` folder.
- **Why:** The two wrestlers are original characters, so matching free sprite sets are unlikely. The generator needs its inputs inside `Assets/`, which D-009's staging rule didn't allow.
- **Affects:** .claude/agents/art-wrangler.md, docs/ARCHITECTURE.md (folder ownership), docs/ASSET_REGISTRY.csv (concept art rows).

## D-011: Craftpix license accepted

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Craftpix's free-asset license is accepted alongside CC0 and CC-BY. Closes Q-005.
- **Why:** Craftpix uses its own license rather than a Creative Commons one, which D-008 didn't cover.
- **Affects:** .claude/agents/art-wrangler.md.

## D-012: Barry's tattoo side and palette flexibility

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Barry's heart tattoo is on his left arm, matching the concept art. The color palette guides the overall look, but not every color must come from it; skin tones are the main exception.
- **Why:** The Style Guide said right arm, but the concept art shows it on his left. Snapping skin to the 14-color palette made it flat yellow-orange.
- **Affects:** docs/STYLE_GUIDE.md (Barry characteristics, Color Palette), .claude/agents/art-wrangler.md.

## D-013: Native resolution and sprite scale

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** The game renders at a native 960×540, scaled by whole numbers to the display (×2 at 1080p, ×3 at 1440p, ×4 at 4K). Barry's standing sprite is 240 px tall (about 480 px on a 1080p screen). Peaux's is 215 px. Sprites import at 60 pixels per unit, so the 960×540 screen is 16×9 world units.
- **Why:** The game targets 1080p monitors, and the designer wants more visible sprite detail than the 112 px and 160 px options gave.
- **Affects:** docs/STYLE_GUIDE.md (Sprite Scale), docs/ARCHITECTURE.md (3.1 Rendering scale), .claude/agents/art-wrangler.md, ProjectSettings (1920×1080 default window). The camera setup is superseded by D-017 (smooth-zooming `MatchCamera` instead of `PixelPerfectCamera`); native resolution, sprite heights and PPU still stand.

## D-014: Barry has no tattoo

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** The heart tattoo is removed from Barry's design entirely. This replaces the tattoo part of D-012; D-012's palette rule still stands.
- **Why:** Mirroring the sprite to face right would move the tattoo to the other arm. Removing it avoids drawing separate right-facing art.
- **Affects:** docs/STYLE_GUIDE.md (Barry characteristics), .claude/agents/art-wrangler.md. The concept art (style-guide-02) still shows the heart; ignore it.

## D-015: Staging is not committed

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** `Staging/` is in `.gitignore`. Candidate art and audio stay local until approved; approved files are committed from `Assets/`. Provenance for every candidate is still recorded in `docs/ASSET_REGISTRY.csv`, which is committed.
- **Why:** Keeps unreviewed candidates and mockups out of the repository.
- **Affects:** .gitignore, .claude/agents/art-wrangler.md.

## D-016: Arena look and HUD style

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** The arena uses the look of `arena_04_golden_a`: Arena 4's pixel style and layout (trailer along the back, truck left, pool right) in late-afternoon golden-hour light. The HUD uses the "Clean Retro" style: diamond-framed portraits, chunky health bars with a white damage trail showing the last hit, names above the bars, and a large plain timer at top center.
- **Why:** Chosen from the arena and HUD mockup rounds (`Staging/Art/arena_mockups_round2/`).
- **Affects:** docs/STYLE_GUIDE.md (Arena, UI).

## D-017: Scrolling, zooming match camera

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:**
  - The arena is wider than the screen. At the default zoom, 60% of the arena's width is on screen; the camera scrolls left and right to follow the wrestlers. The arena's full depth (top to bottom) fits on screen at default zoom.
  - When the wrestlers move apart and reach the screen edges, the camera zooms out smoothly to show more of the arena, up to the whole arena. As they come back together it zooms in until it returns to the default view.
  - The default visible fraction (0.6), the maximum zoom-out (whole arena), and the zoom behavior are Inspector-editable values.
  - Zoom is smooth. The default view stays pixel-perfect (×2 at 1080p); while zoomed out, pixels may render at slightly uneven sizes.
- **Why:** Gives the arena room to move around in, like WrestleFest's scrolling ring, while keeping both wrestlers in view.
- **Affects:** docs/ARCHITECTURE.md 3.1 (replaces the PixelPerfectCamera setup from D-013), docs/STYLE_GUIDE.md (Arena). Arena art must cover the fully zoomed-out view: 1600×900 native pixels (see ARCHITECTURE 3.1).

## D-018: Production arena and layout

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** The arena uses backdrop B (bigger trailer, satellite dish) with the truck (back-left) and pool (back-right) as separate obstacle sprites, in `Assets/Art/Arena/`. The layout in ARCHITECTURE 3.1 is approved: default view rows 300–840, walkable feet rows 580–820, trailer as the back wall, truck and pool depth-sorted with the wrestlers. The backdrop is slightly soft from being cropped and scaled; a sharper regeneration can come later.
- **Why:** Chosen from the production arena mockups (`Staging/Art/arena_prod_mockups/`).
- **Affects:** docs/ARCHITECTURE.md 3.1, docs/ASSET_REGISTRY.csv.

## D-019: Keyboard layout and menu controls

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Keyboard: Move = WASD, Strike (X) = J, Lock Up/Grapple (A) = K, Run/Whip (B) = Space. No Shift keys. Menus: move the selection with the stick, D-pad, WASD or arrows; confirm with A or Start (Enter or Space on keyboard); go back with B (Esc on keyboard). Replaces D-002's Shift bindings; closes Q-001 and Q-002.
- **Why:** Mashing Shift during knockdowns would open the Windows Sticky Keys prompt. Menu buttons were undefined.
- **Affects:** docs/CONTROLS.md, docs/ARCHITECTURE.md (4.1), .claude/agents/ui-match-flow.md.

## D-020: Running is press-once

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** One press of Run starts a run in the facing direction (left or right). It continues until the wrestler hits an obstacle or the arena edge, gets hit, or performs another action. Facing up or down, the run goes toward the last left/right facing.
- **Why:** ARCHITECTURE gap #5; matches WrestleFest.
- **Affects:** docs/CONTROLS.md, docs/COMBAT_SPEC.md (Running), docs/ARCHITECTURE.md (gaps).

## D-021: Architecture proposals approved

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Approves the Architect's proposals in ARCHITECTURE.md, reviewed one by one: one scene per screen loaded singly; the `Wrestler` input map (`Move`, `LockUpGrapple`, `RunWhip`, `Strike`); presentation-only wrestler prefabs; a shared Animator graph with per-wrestler clips (each wrestler keeps its own animations; wrestler-exclusive moves are post-MVP); combat-driven attack timing; code-based hit boxes; per-frame combat tick with a capped step; UI Toolkit for screens and HUD; a static `GameSession` for the selected wrestlers; Juice Agent plays SFX and music. The CPU always gets the wrestler the player didn't pick. `SampleScene.unity` is to be deleted (the designer authorized this) when the new scenes are created.
- **Why:** Unblocks the first code work.
- **Affects:** docs/ARCHITECTURE.md (all former "Proposed" items, gaps 11–13).

## D-022: Match assembly for MatchBootstrap

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** `MatchBootstrap` moves out of `RuralRumble.Core` into a new Systems Architect-owned `RuralRumble.Match` assembly (`Assets/Scripts/Match/`) that references Core, Combat, AI and Presentation.
- **Why:** MatchBootstrap must build `CombatMatch` and `OpponentBrain`, but Core may not reference Combat or AI (ARCHITECTURE conflict 7). No gameplay effect.
- **Affects:** docs/ARCHITECTURE.md (section 2 assembly table, 4.4, conflict 7).

## D-023: Recovering and Knocked Out animations

- **Date:** 2026-10-30
- **Decided by:** Designer
- **Decision:** Recovering gets its own animation, modeled on WrestleFest's: the wrestler clutches their ribs while standing and walks slowly. Knocked Out reuses the Knocked Down animation; the wrestler just doesn't get up.
- **Why:** The Style Guide had no Recovering example, and COMBAT_SPEC already describes Knocked Out as the Knocked Down animation without getting up.
- **Affects:** docs/STYLE_GUIDE.md (Sprite Sheet & Animation Examples), Barry and Peaux animation lists.

## D-024: Strikes and Struck

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - A strike can land on a wrestler in the Struck state, so the 1-2-3 combo connects. Each hit restarts Struck; Strike 3 knocks down.
  - Struck lasts 0.4 s (tunable).
  - Strike timing starting values match the approved art: wind-up = time to the contact frame (Strike 1 and 2: 0.21 s, Strike 3: 0.125 s), hit window 0.1 s, recovery 0.15 s (Strike 3: 0.25 s). All tunable.
  - Wrestlers can't move while striking. Striking ends a run (D-020: another action).
- **Why:** COMBAT_SPEC's list of states a strike can land on omitted Struck, which would stop combos from connecting. The other values were unspecified (ARCHITECTURE gaps 1 and 6).
- **Affects:** docs/COMBAT_SPEC.md (Striking/Combo Rules, Tuning Values), docs/ARCHITECTURE.md section 8.

## D-025: Knockdowns and the ground stomp

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - Knockdown length scales linearly with health lost, from minimum knockdown time at full health to maximum near 0.
  - The CPU mashes to shorten knockdowns at a tunable rate, under the same rule as the player.
  - A downed wrestler can be hit, but only by a new **Stomp** move: pressing Strike while standing over a Knocked Down opponent stomps instead of punching. A stomp deals damage only (tunable, starting at 3), doesn't change the knockdown timer, and can knock out. There is no limit on stomps per knockdown.
  - No invulnerability after getting up.
  - Stomp cooldown: after a stomp animation ends, the same wrestler can't stomp again for 1.0 s (tunable in the Inspector), so stomps can't be chained rapidly.
- **Why:** ARCHITECTURE gaps 7 and 8; hitting a downed wrestler was not in the spec.
- **Affects:** docs/COMBAT_SPEC.md, docs/CONTROLS.md (Strike), docs/STYLE_GUIDE.md (new Stomp animation for each wrestler), docs/ARCHITECTURE.md (`MoveId` gains `Stomp`).

## D-026: Match start and end

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - The "{Player} vs {CPU}" banner shows for about 2 s while both wrestlers stand still. Control and the timer start when it ends.
  - The timer counts up from 0:00 and displays as m:ss. There is no time limit.
  - On a knockout, everything freezes for about 1.5 s, then the Victory screen loads.
  - The Victory screen has one option: any button returns to the Start Screen.
- **Why:** ARCHITECTURE gaps 2 and 3.
- **Affects:** UI & Match Flow work, docs/ARCHITECTURE.md section 8.

## D-027: Movement and arena rules from the movement playtest

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - The walkable area is narrowed at the left and right ends so a wrestler's whole sprite stays on the arena art. The camera is unchanged.
  - Wrestlers block each other: their feet can't overlap.
  - A run that hits the truck or pool puts the runner in Struck and deals obstacle collision damage. The trailer/fence line and the arena ends just end the run, without damage.
  - A player who started their own run can cancel it by pressing Run again. A run caused by an Irish Whip can't be cancelled this way (see D-028).
  - ~~Walking is the same speed in every direction.~~ Superseded by D-029: three tunable walk speeds.
- **Why:** Issues found in the movement-slice playtest; ARCHITECTURE gap 10.
- **Affects:** docs/CONTROLS.md (Running), docs/COMBAT_SPEC.md (Running), docs/ARCHITECTURE.md section 8, arena prefab bounds.

## D-028: Lock-ups and Irish Whips

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - A lock-up can target an opponent who is Standing, Walking, Recovering, or Striking before the strike lands (COMBAT_SPEC's list). CONTROLS.md's "standing opponent" wording is corrected to match.
  - If both wrestlers attempt a lock-up in range on the same tick, the winner is chosen 50/50 at random.
  - A whipped wrestler has no control at all during the first 3 s of the whip run (tunable): no strikes, lock-ups or cancelling. After that they can cancel the run by pressing Run.
  - The whipping wrestler returns to normal control as soon as their Irish Whip animation finishes; what they do next is up to the player or CPU.
  - A strike on a whipped wrestler deals that strike's normal damage, not Irish Whip impact damage.
- **Why:** ARCHITECTURE conflict 1 and gaps 4 and 9.
- **Affects:** docs/CONTROLS.md, docs/COMBAT_SPEC.md (Lock Up/Grappling/Irish Whip Rules), docs/ARCHITECTURE.md section 8.

## D-029: Running, walk speeds, and knockdown check

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - While running, Strike is allowed: it ends the run and throws Strike 1 on the spot. Lock Up is not allowed while running. Run cancels your own run (D-027).
  - Walk speeds stay as playtested (3.0 sideways, 2.0 toward/away from the camera, about 3.6 on diagonals), but are three separate Inspector values: horizontal, vertical, and diagonal walk speed. This replaces D-027's "same speed in every direction".
  - A running wrestler who runs into the opponent knocks both of them down. Each gets their normal knockdown (length from their own health). No damage is specified, so run-collision damage is a tunable value starting at 0.
  - The "below 30 health" knockdown check for a strike is made after the hit's damage is applied.
- **Why:** ARCHITECTURE conflicts 8 and 9 and gaps 14 and 15, found when preparing the combat-core contracts.
- **Affects:** docs/COMBAT_SPEC.md, docs/CONTROLS.md, docs/ARCHITECTURE.md (sections 4.1, 4.2, 5, 8), CombatTuning.

## D-030: Double knockouts, crashes, and downed wrestlers

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - If both wrestlers reach 0 health on the same tick, the match is a **draw**. The Victory screen needs a draw message (UI step).
  - A wrestler who runs into the truck or pool and is below 30 health after the obstacle damage is knocked down, the same as a strike or whip impact.
  - A running wrestler passes over an opponent who is lying on the ground (Knocked Down or Knocked Out). Downed wrestlers don't block runs.
  - The CPU keeps a tunable chance (starting at 30%) to strike back while Recovering if the player is in range.
  - Defaults from the combat build stay: the combo window is measured from the previous strike's press; strikes whose hit windows open on the same tick both land; Strike during the stomp cooldown over a downed opponent does nothing; run-collision damage applies to both wrestlers; "standing still" for regeneration means no move input.
- **Why:** Gaps found while building the combat rules (combat-core slice step 2).
- **Affects:** docs/COMBAT_SPEC.md, docs/ARCHITECTURE.md (`MatchResult` gains a draw), UI Victory screen.

## D-031: Lock-up whiffs and controls during a lock-up

- **Date:** 2026-10-31
- **Decided by:** Designer
- **Decision:**
  - Pressing Lock Up with no valid opponent in range is a whiff: the wrestler grabs at air for a short, tunable time (starting at 0.4 s), rooted and able to be hit, then returns to Standing or Recovering.
  - The locked-up wrestler (Wrestler B) has no control during the lock-up. Reversals stay future work.
  - During the lock-up, the initiator's Strike button is ignored. Only Grapple (A) and Irish Whip (B) act; otherwise the lock-up timer runs out and the lock-up breaks.
- **Why:** COMBAT_SPEC's Lock Up state covers "attempting to trigger" a lock-up but never said what a miss does, or which buttons work during a lock-up. Found when starting the lock-up slice.
- **Affects:** docs/COMBAT_SPEC.md (Lock Up/Grappling/Irish Whip Rules, Tuning Values), docs/CONTROLS.md, CombatTuning.

## D-032: Lock-up art in game

- **Date:** 2026-11-02
- **Decided by:** Designer
- **Decision:**
  - The v5 Lock Up and Locked Up sheets for Barry and Peaux (gen-*-lockup-5, gen-*-lockedup-5) are approved and go into the game.
  - The art is drawn for hips 2.2 units apart, but combat's `lockUpPairSpacing` stays 1.2. The lock-up sprites' pivots are moved 30 px toward the opponent so the drawn pair lines up. This is presentation only: no gameplay change.
  - Lock-up whiff and break keep their stand-in clips until they get art.
  - All three frames of all four sheets are scaled to 90% of the original art (both partners together, around the point where they meet), because in game the wrestlers seemed to grow after frame 1. This replaces an earlier try that scaled only frames 2 and 3 to 85%.
- **Why:** The art needs the wider spacing, and widening the combat spacing would push the receiver about 1.2 units back on every lock-up.
- **Affects:** docs/ASSET_REGISTRY.csv, WrestlerAnimationBuilder, Barry and Peaux lock-up clips.

## D-033: Bodyslam art in game

- **Date:** 2026-11-02
- **Decided by:** Designer (art, timing, spacing); presentation wiring by Unity Animation & Tooling, for review
- **Decision:**
  - The v3 bodyslam sheets are approved and go into the game: Barry slams Peaux (gen-barry-bodyslam-3 + gen-peaux-bodyslammed-3, 7 frames, 300/140/430/130/230/700/500 ms) and Peaux slams Barry (gen-peaux-bodyslam-3 + gen-barry-bodyslammed-3, 6 frames, 300/430/130/230/700/500 ms, the old frame 2 removed). Each plays once with its partner clip, last frame held.
  - Like the lock-up art, the pair is drawn with pivots 132 px (2.2 units) apart, so every bodyslam sprite's pivot is moved 30 px toward the opponent (D-032) and the pair lines up at combat's `lockUpPairSpacing` of 1.2. The receivers keep their drawn ground line (Peaux 4 px, Barry 1 px above the cell bottom).
  - The sheets are wider than 2048 px, so their max texture size is 4096 (not downscaled). Same wrestler import settings otherwise.
  - Presentation wiring: Grappling + Move Bodyslam plays Bodyslam (initiator), Grappled + Move Bodyslam plays Bodyslammed (receiver). Both clips are in the MoveAnimationTable with the impact frame (receiver flat on the ground: frame 6 of 7, frame 5 of 6) as contact time, so the impact lands at the Bodyslam Startup. The initiator draws in front, as in the approved previews.
- **Why:** Designer approval of the v3 sheets and previews (ASSET_REGISTRY rows). Reusing the lock-up spacing means combat does not have to move either wrestler when the bodyslam starts from the lock-up.
- **Affects:** docs/ASSET_REGISTRY.csv, docs/ARCHITECTURE.md (3.1, 6), WrestlerAnimationBuilder, WrestlerView, Barry and Peaux bodyslam clips, Wrestler.controller.

## D-034: Audio sourcing policy; Sound Wrangler and Juice Agent created

- **Date:** 2026-11-11
- **Decided by:** Designer
- **Decision:** Audio (sound effects and music) may come from Freesound.org, OpenGameArt.org, Kenney.nl, and itch.io free assets. Accepted licenses: CC0 and CC-BY (exact attribution recorded); NonCommercial, NoDerivs, and unstated licenses are not allowed. When sourcing finds nothing suitable, AI-generated audio is allowed as a marked placeholder that the designer must review; prompts describe genre and feel and never name real artists, songs, or lyrics. Ripped or copyrighted commercial music and sound effects are never allowed. The Sound Wrangler and Juice Agent are created now (`.claude/agents/sound-wrangler.md`, `.claude/agents/juice-agent.md`); D-021's split stands: the Sound Wrangler provides files, the Juice Agent plays them.
- **Why:** D-008 covered art only, and CLAUDE.md requires permitted sources for every asset. Sound effects are blocked until audio files exist (goal agent: `sound_effects` gated on `Assets/Audio/`).
- **Affects:** .claude/agents/sound-wrangler.md, .claude/agents/juice-agent.md, docs/ARCHITECTURE.md (folder ownership no longer "future"), docs/ASSET_REGISTRY.csv (new `audio` category).
- **Amendment (2026-11-11, designer):** Audio bought by the designer from the **Unity Asset Store** under its standard license is also allowed (first purchase: Gamemaster Audio "Pro Sound Collection"). Because the repository is public and that license does not permit redistribution, store audio is never committed: approved files go in `Assets/Audio/ThirdParty/` (git-ignored), every file used still gets an `ASSET_REGISTRY.csv` row (license `Unity Asset Store EULA`), and the root README lists the packs a cloner must buy and import. The game must run without them (missing clips are skipped). The first-pass CC0/CC-BY candidates were rejected by the designer (too clean and generic, light hits, voices and menu sounds off-style).

## D-035: Truck and pool moved down onto the ground

- **Date:** 2026-11-11
- **Decided by:** Designer
- **Decision:** The pool sat at the porch steps ("appears to be on the porch") and the truck looked like it was floating. In `Arena.prefab` the designer set exact Transform positions: **pool (21, −14)** and **truck (−2, −13)** (from (18.3333, −10.6667) and (−0.6667, −10.8333)). Both now sit in the foreground; part of each extends past the arena art's edge (the pool's right third is never on camera). Collision moves with them (their `ArenaObstacleMarker`s are on the same objects): wrestlers walk behind both, through a 0.67-unit lane in front of the truck, and pass the pool only from behind. Changes the D-018 layout only for these two positions; footprint sizes, the walkable band, and spawn points are unchanged.
- **Why:** The obstacles should look grounded in the dirt yard while still blocking movement.
- **Affects:** Assets/Prefabs/Arena/Arena.prefab, docs/ARCHITECTURE.md (3.1 arena table), Assets/Tests/PlayMode/ArenaPrefabTests.cs.
- **Playtest (2026-11-12):** designer accepted the obstacle look and walking around them.

## D-036: Faster bodyslam (retimed clips and impact times)

- **Date:** 2026-11-12
- **Decided by:** Designer
- **Decision:** The designer retimed all four bodyslam clips with the new millisecond Frame Timing tools and set each slammer's impact time (`CombatTuning.bodyslamStartupByInitiator`) to match: **Barry 0.525 s** (was 1.208) and **Peaux 0.475 s** (was 1.083). Bodyslam damage now lands at that time, roughly twice as fast as under D-033. Barry's slam plays at 1x; Peaux's clip puts its impact at 500 ms, so it plays about 1.05x (impact lands 25 ms before the clip's HIT sprite at native speed).
- **Why:** The D-033 art timing felt too slow in play.
- **Affects:** Assets/Animation/*/{Barry,Peaux}_FrameTiming.asset and the four bodyslam clips, Assets/Animation/MoveAnimationTable.asset, Assets/Data/Tuning/CombatTuning.asset, docs/ARCHITECTURE.md, docs/ACCEPTANCE_TESTS.md (L5.9). EditMode `BodyslamTests` keep their own fixture values (they test the per-initiator rule, not the project asset).
- **Playtest (2026-11-12):** designer accepted the faster bodyslam and the Frame Timing Inspector (including the combat-timing section and the Disable Cpu Ai testing toggle).

## D-037: Focus: wrestlers always face each other

- **Date:** 2026-11-12
- **Decided by:** Designer
- **Decision:**
  - **Facing:** while a wrestler is **free to act** (Standing, Walking or Recovering), their left/right facing always points at the opponent, whatever direction they move. Walking away means backing up while still facing the opponent. If the two are exactly vertically aligned, the last left/right facing is kept.
  - **Front/back stance** comes from position, not input. If the opponent is more than the **Focus back-stance height** higher on screen (farther back, toward the trailer), the wrestler uses the **back** stance; otherwise **front**. At about equal height, both use front. This holds whichever side each wrestler is on.
  - **Tunable:** `CombatTuning.focusBackStanceHeightPx`, default **50 px** (60 px = 1 world unit; first set at 100 px, lowered to 50 px by the designer the same day after trying it in game), editable in the Inspector and live in Play Mode.
  - **Locked facing:** while striking, Struck, Knocked Down / Knocked Out, or in a lock-up or bodyslam, facing stays where it was. It snaps back to the opponent as soon as the wrestler is free to act again.
  - **Running:** Focus is off for the runner, who faces the run direction until the run ends. **Run direction:** the left/right direction held on the Left Stick/D-Pad (or A/D) when Run is pressed; with no left/right held, **toward the opponent**. A wrestler whose opponent is running (but who isn't running) keeps focusing on them.
  - **Input:** movement input moves the wrestler but no longer turns them or chooses the stance. This applies to the player and the CPU alike.
- **Why:** Keeps both wrestlers engaged with each other like a real match, and makes the stance read clearly from where the wrestlers stand.
- **Affects:** docs/CONTROLS.md (movement, Run), docs/COMBAT_SPEC.md (Focus, Tuning Values), Combat (`CombatWrestler` facing, run direction), `CombatTuning`, EditMode/PlayMode tests that assumed facing follows input. Presentation is unchanged: it already draws the facing combat reports.
- **Open point:** Q-006 (Irish Whip direction under Focus).
- **Playtest (2026-11-12):** designer accepted Focus (facing, stance at 50 px, running, locked facing, CPU).

## D-038: One downed sprite for every knockdown; shorter bodyslam recovery

- **Date:** 2026-11-12
- **Decided by:** Designer
- **Decision:**
  - **Downed sprite:** every Knocked Down / Knocked Out shows the wrestler's **last Bodyslammed sprite** (replacing the rotated standing-sprite stand-in). After a bodyslam the wrestler stays exactly as they landed, head toward the slammer. Every other knockdown (strike, run collision, obstacle, knockout from standing) shows the same sprite mirrored in place, so the body lies **feet toward the opponent**. There is no fall animation yet.
  - **Bug fixed:** a bodyslammed wrestler used to swap head and feet when the Knocked Down clip started (the old stand-in pose). Combat was not involved.
  - **Bodyslam Recovery 1.11 → 0.3 s** (`CombatTuning.bodyslamTiming`): both wrestlers act again much sooner after a bodyslam. The receiver is knocked down about 0.4 s after impact, so the rest of the Bodyslammed clip is cut short by the (seamless) switch to the same downed sprite.
- **Why:** Reuse existing art for every knockdown, fix the visible flip, and speed up the pace after a slam.
- **Affects:** WrestlerAnimationBuilder, WrestlerView (`downedMirrorShift` on the wrestler prefabs), the KnockedDown clips and FrameTiming entries, CombatTuning.asset, docs/ARCHITECTURE.md §6.
- **Playtest (2026-11-12):** designer accepted.

---

## D-039: Irish Whip direction, choreography and art

- **Date:** 2026-11-13
- **Decided by:** Designer
- **Decision:**
  - **Direction (resolves Q-006):** with no left/right held, the whipped wrestler runs the way they faced in the lock-up, which carries them **past the whipper**. Holding left or right whips that way (left/right component of the stick or D-pad; up/down alone counts as no direction). The CONTROLS.md wording ("the direction the opponent is currently facing") stands.
  - **Choreography** (designer's reference `Staging/Art/wrestler/Irish Whip Mechanics in WWF Superstars.pdf`, used as a written motion description only): four whipper frames, then stand. f1 push start: whipper still faces the opponent, pushing arm hidden behind the runner. f2 turn: whipper faces the whip direction, mostly covered by the runner. f3 final push: pushing arm goes into the runner's back and is hidden by the runner. f4 follow-through: runner clear, open hand near the runner's back. The same arm pushes in every frame. The runner is drawn in front of the whipper and faces/moves in the whip direction from f1. Both wrestlers' facing follows the whip direction. No part of the whipper's arm may show past the runner in f1–f3. The whip ends when the runner is about 1.5 body widths past the whipper's hand; the whipper is then free, facing the whip direction.
  - **Art:** the 4-frame whipper candidates `gen-barry-irishwhip-4` and `gen-peaux-irishwhip-4` are approved.
  - **Timing** is designer-tunable like the other moves: per-frame holds in each wrestler's FrameTiming asset, and the whip's combat timing in CombatTuning (shown in the same Inspector).
- **Why:** The first 3-frame whip art switched pushing arms, didn't flip the whipper with the whip direction, and showed the arm past the runner. The reference document defines how the frames connect.
- **Affects:** docs/CONTROLS.md, docs/COMBAT_SPEC.md, docs/ARCHITECTURE.md §6, docs/ASSET_REGISTRY.csv, docs/CREDITS.md, Combat (LockUpController, CombatWrestler, CombatTuning, OpponentBrain), presentation (whip clips, FrameTiming, WrestlerView).
- **Playtest (2026-11-14):** designer accepted the Irish Whip. Credits: one entry covers all AI-generated sprites (designer, 2026-11-14).

---

## D-040: Struck B for every non-knockdown hit

- **Date:** 2026-11-15
- **Decided by:** Designer
- **Decision:** Barry and Peaux show their Struck B sprite (upright head snap) for every hit that doesn't knock them down. The two reactions no longer alternate. The Struck A sprites (lean back) and clips stay in the project, unused.
- **Why:** Both Struck A sprites lean back too far.
- **Affects:** WrestlerView (presentation only), CombatPresentationTests, docs/ARCHITECTURE.md, docs/ACCEPTANCE_TESTS.md. No combat rule or timing change.

---

## D-041: Falling Down animation and knockback slide

- **Date:** 2026-11-15
- **Decided by:** Designer
- **Decision:**
  - **Animation** (designer's spec `Staging/Art/wrestler/Falling Down Mechanics .pdf`; the WWF WrestleFest images in it are a motion reference only, never generator input): struck_a → Fall 2 (airborne) → Fall 3 (level, about to land) → the downed sprite, held. Defaults 100 / 120 / 130 ms, tunable per frame in each wrestler's FrameTiming. The head always falls away from whatever hit the wrestler and the feet point toward it; they never swap sides during the fall.
  - **When it plays:** every non-bodyslam knockdown (Strike 3, a strike below the Recovering threshold, a truck/pool crash below the threshold, a run collision for both wrestlers) and a knockout from standing. A bodyslam keeps its own animation; a stomp KO while already down doesn't fall again.
  - **Slide:** during the fall the wrestler slides 0.5 units (`knockdownSlideDistance`) over 0.35 s (`knockdownSlideDuration`) on x, away from the attacker, the other runner, or the obstacle, with normal movement clamping. Knockdown length and damage are unchanged.
  - **Knockout slide:** after a knockout, only the knocked-out wrestler finishes the slide; the winner, timers, states and events stay frozen (exception to ARCHITECTURE §4.2's freeze).
  - **Stomp range stays 2.0:** the slide can carry the victim out of stomp range; stepping in to stomp is intended.
  - **Art:** Barry Fall 2 = `gen-barry-fall2-D-1`, Fall 3 = `gen-barry-fall3-B-1` (approved AI placeholders, flagged for review). Peaux Fall 2 = `gen-peaux-fall2-D-1`, Fall 3 = `gen-peaux-fall3-C-1` (designer-picked 2026-11-15; approved AI placeholders, flagged for review), same holds and landing as Barry.
- **Why:** Knockdowns snapped straight to the downed sprite with no motion (D-038 said "no fall animation yet").
- **Supersedes:** D-038's "mirrored in place, no fall animation" for non-bodyslam knockdowns.
- **Affects:** docs/COMBAT_SPEC.md, docs/ARCHITECTURE.md §4.2 and §6, docs/ACCEPTANCE_TESTS.md, docs/ASSET_REGISTRY.csv, Core (`IWrestlerView.FallSide`), Combat (CombatWrestler, CombatMatch, CombatTuning), presentation (WrestlerView, WrestlerAnimationBuilder, Fall clips, FrameTiming).

---

## D-042: Struck B on combo hit 1, Struck C (head turn) on hit 2

- **Date:** 2026-11-15
- **Decided by:** Designer
- **Decision:**
  - A third hit reaction, Struck C: the wrestler stays upright and his head turns away from the camera (WWF WrestleFest `Staging/Art/wrestler/Struck Head Turn.png` as a motion reference only, never generator input).
  - A Strike 1 hit that doesn't knock down shows Struck B. A Strike 2 hit shows Struck C. (The designer first placed Struck C on hit 1, then swapped the order the same day.) Strike 3 knocks down and plays the fall (D-041), as before.
  - Any other non-knockdown Struck (for example a truck or pool crash above the Recovering threshold) shows Struck B.
  - Every hit restarts the reaction from its first frame (D-024).
  - **Art:** Barry = `gen-barry-struck-c-E-1` (designer chose E, face in profile looking up toward the attacker). Peaux = `gen-peaux-struck-c-A-2` (accepted with the chin up and slightly left; reads as the head turned away with the face hidden). Both approved AI placeholders, flagged for review.
- **Why:** Designer request (2026-11-15): a straighter, head-turn reaction that doesn't lean back, within the combo.
- **Supersedes:** D-040's "Struck B for every non-knockdown hit" for Strike 2 hits.
- **Affects:** WrestlerView and WrestlerAnimationBuilder (presentation only), Struck C clips and FrameTiming, CombatPresentationTests, docs/ARCHITECTURE.md, docs/ACCEPTANCE_TESTS.md, docs/STYLE_GUIDE.md, docs/ASSET_REGISTRY.csv. No combat rule or timing change.

---

## D-043: Crouch get-up phase

- **Date:** 2026-11-15
- **Decided by:** Designer
- **Decision:**
  - Every knockdown ends with a short get-up phase: when the knockdown timer runs out, the wrestler shows the crouch for the **Get-up time** (`getUpTime`, 0.25 s, Starting point) and can't act yet, then returns to Standing or Recovering as before. Every knockdown, including the bodyslam receiver's, is 0.25 s longer.
  - The get-up is part of the Knocked Down state: no control, mashing affects only the knockdown timer (not the get-up), only a Stomp can hit (stomp rules unchanged, a Stomp can still knock out), facing stays locked, and the CPU treats him as down. No new events. `IWrestlerView` adds `IsGettingUp`, `GetUpTimeRemaining` and `GetUpDuration` for presentation; `KnockdownTimeRemaining` still means the mashable timer only.
  - **Presentation:** the get-up shows the crouch (`barry_crouch.png`, `peaux_crouch.png`; a reusable Animator state `Crouch`, slot `Slot_Crouch`, clip `<Wrestler>_Crouch.anim`) for the whole get-up, drawn by the combat facing at the wrestler's root (child sprite offset 0, the same anchor as the stand), then the stand as before; crouch → stand lines up on the front boot. **No slide.** The first version slid the crouch from over the lying body to his feet (the designer's earlier choice: "Slide the crouch. The crouch starts drawn over where the lying body was and slides to his feet during the get-up. No new art is needed."); after playtesting it (2026-11-15) the designer removed the slide: "After testing, I don't want any slide on the crouch. I just want them to get up in the place that they are." A knockout has no get-up; a Stomp KO during the get-up returns to the lying pose.
- **Why:** Designer request (2026-11-15): "Short get-up phase in combat. Add about 0.25 s after the knockdown where he shows the crouch and can't act yet. The crouch always shows in full, but every knockdown gets 0.25 s longer."
- **Affects:** docs/COMBAT_SPEC.md (Knocked Down, Tuning Values), docs/ARCHITECTURE.md §4.2, Core (`IWrestlerView`), Combat (CombatWrestler, CombatTuning, CombatTuning.asset), EditMode tests (KnockdownTests and timing that assumed an instant get-up), PlayMode `PresentationTests` fake view.

---

## D-044: Bodyslam throw (Barry slams Peaux, v5 art)

- **Date:** 2026-11-16
- **Decided by:** Designer (throw, distance, variance, blocking, shared downed pose, rollout, bounce, no foot jump); values marked "Starting point" were set by the Systems Architect for the designer to retune
- **Decision:**
  - **Art:** Barry slams Peaux uses the new 12-frame v5 animation, modelled on the designer's `Staging/Art/wrestler/Bodyslam Mechanics.pdf` (WWF Superstars, motion reference only, never generator input): Barry (slammer) S1–S5 `Assets/Art/Wrestlers/Barry/barry_bodyslam5_1..5.png`; Peaux (slammed) R1–R7 `Assets/Art/Wrestlers/Peaux/peaux_bodyslammed5_1..7.png`, played R1…R7 then R6 held. Cell geometry, pivots and flip rules: `Staging/Art/wrestler/gen-bodyslam-pair-previews-5/bodyslam5_cells.json` (`_notes`; the promoted S1/S2/R1/R2 are the v5c cells). Frames: S1/R1 and S2/R2 = the lift, a pair at the lock-up spacing (art 132 px apart, 30 px pivot shift, D-032/D-033), Barry facing Peaux; S3/R3 = Peaux overhead, **Barry has turned to face the throw direction**; S4/R4 = release, Peaux airborne as a separate sprite; S5/R5 = Peaux about to land; R6 = first contact with the mat (impact); R7 = bounce; R6 again, held. After S5 Barry returns to his normal stand, facing the throw direction.
  - **Throw:** "This body slam will also send the slammed wrestler in the direction they are facing when locked up." The receiver faces the slammer in the lock-up, so he is carried over and thrown **past the slammer**, landing on the slammer's far side. The throw is along x only, at the lock-up depth.
  - **Distance:** "The distance that the bodyslammed wrestler is thrown should differentiate by give or take 10%… Use the distance depicted in this guide as the median… once the bodyslammed wrestler is free of the bodyslammer's grip, they should react as two separate sprites." Measured median: **1.59 × the slammer's standing height**, from the slammer's feet centre to the landed receiver's body centre. For Barry (240 px, 60 PPU) that is 382 px = 6.37 units from his feet; (first set as 412 px root to root for a 30 px feet offset; changed to **382 px root to root** by the follow-up below, since Barry's feet are drawn at his root from S3 on). Variation: **uniform ±10%** around the median, drawn once per slam.
  - **Blocked throw** (designer chose "Stop short + crash damage"): if the landing spot is past the arena edge, or the flight path reaches the truck or pool, the flight stops at contact with normal movement clamping (like the D-041 knockback slide). Reaching the truck/pool also adds the **obstacle collision damage** (8). The arena edge adds no extra damage.
  - **Damage:** bodyslam damage **12** at landing (unchanged value), **+8** (`obstacleCollisionDamage`) if the throw hit the truck or pool.
  - **Receiver:** Knocked Down **from the landing** (the impact): the normal health-based knockdown plus the D-043 get-up. No D-041 fall and no knockback slide.
  - **Slammer free on landing:** the guide shows the slammer "back in his front standing state and free to move" at R6, so the slammer returns to Standing or Recovering at the landing.
  - **Facing after landing:** the existing rules. The receiver's facing stays locked while down. The slammer turns to the throw direction at S3 (presentation), which is toward where the receiver lands; Combat sets his facing to the throw direction when the move ends, and Focus then keeps him facing the receiver.
  - **Shared downed pose ("Replace everywhere"):** the new R6 becomes **Peaux's** downed sprite for every knockdown and knockout (replaces D-038/D-041's "last Bodyslammed sprite" for Peaux). Barry keeps his current downed sprite until his own new art exists.
  - **Bounce:** R7 bakes in a 17 px lift. Pure art, no combat effect.
  - **No foot jump:** Barry's feet must not visibly jump 30 px when he goes from S5 back to the stand (presentation fix; S5's feet are 30 px from his root, the stand's feet are at the root).
  - **Rollout:** Barry slams Peaux goes in first, for playtesting. **Peaux slams Barry keeps its old animation and old behaviour (no throw)** until its own new art exists; that art is drawn with the receiver landing in place. The throw is therefore enabled **per slammer** (like `irishWhipByWhipper`): Barry on, Peaux off. Kept minimal: nothing more general than the two wrestlers need.
  - **Starting point (timing):** Barry's new clip holds S1 120, S2 120, S3 180, S4 100, S5 100 ms (R1–R5 the same), so the **release is at the start of S4/R4, 0.42 s**, and the **landing/impact at 0.62 s** (Barry's bodyslam Startup, was 0.525 under D-036). Then R6 100, R7 150, R6 held. All tunable in the FrameTiming assets and CombatTuning; the designer will retune.
  - **Starting point (flight):** from the release to the landing Combat moves the receiver's root on x only, linearly, from the release offset (R4's drawn spot: 178 px past the slammer's feet = 208 px from the slammer's root) to the drawn landing distance. The ±10% applies to the root-to-root landing distance (applying it to the 382 px feet-to-body part instead would differ by at most 3 px).
  - **Starting point (crash timing):** a truck/pool contact raises `ObstacleHit` on the contact tick; both damages are applied together at the landing.
- **Why:** The designer replaced the Barry-slams-Peaux art with a throw modelled on the arcade reference, wants the throw distance to vary, and wants one lying pose for Peaux. Rolling out one direction first keeps main playable while the Peaux-slams-Barry art is made.
- **Supersedes:**
  - D-033: the Barry-slams-Peaux v3 sheets and their 7-frame timing (Peaux slams Barry v3 stays); "Combat does not have to move either wrestler" no longer holds for a throw (Combat moves the receiver's root from the release to the landing).
  - D-036: Barry's bodyslam impact time 0.525 s → 0.62 s (Starting point) with the new clip. Peaux's 0.475 s and clips are unchanged.
  - D-038: for a thrown bodyslam, the receiver is Knocked Down at the landing, not after Active + Recovery (0.4 s later), and the slammer is free at the landing; the 0.3 s Recovery still applies to Peaux slams Barry. Peaux's downed sprite is R6, not his last v3 Bodyslammed sprite.
  - D-041: Peaux's downed sprite at the end of his fall is R6 (`downedMirrorShift` re-measured). Fall rules unchanged; a bodyslam still never falls or slides.
- **Affects:** docs/COMBAT_SPEC.md (Lock Up/Grappling rules, Knocked Down, Tuning Values), docs/ARCHITECTURE.md §4.2, §4.6, §5, §6, docs/ACCEPTANCE_TESTS.md, docs/ASSET_REGISTRY.csv (v5 rows), Combat (`LockUpController`, `CombatTuning` + `CombatTuning.asset`, `PairedMoveInfo`), presentation (WrestlerAnimationBuilder, WrestlerView, Barry_Bodyslam and Peaux_Bodyslammed clips, Peaux's downed sprite / KnockedDown / Fall clips, FrameTiming, MoveAnimationTable, Peaux prefab `downedMirrorShift`), Juice (no double thud). CPU FSM unchanged.
- **Follow-up (2026-11-16, presentation build):** Barry's S3–S5 are drawn with his feet at his root (the 30 px lock-up shift is taken during the S3 turn, so S5 → stand has no foot jump; R3 shifted to match). The throw values were therefore set to the guide's feet-based distances: `releaseOffsetPx` **178** and `landingDistancePx` **382 px = 6.37 units root to root** (were 208 / 412). Peaux's `downedMirrorShift` re-measured for R6 (2.2833 → −0.1667) and a `fallLandingShift` (Peaux 1.3333, Barry 0) added so his fall lands on R6 without a jump. The D-036 holds for Barry_Bodyslam and Peaux_Bodyslammed reset to the D-044 starting points (frame counts changed).

---

## D-045: Bodyslam throw (Peaux slams Barry, v5 art)

- **Date:** 2026-11-17
- **Decided by:** Designer (art approval, throw, Barry's shared downed pose follows D-044's rule); values marked "Starting point" were measured by Unity Animation & Tooling against the drawn art for the designer to retune
- **Decision:**
  - **Art:** Peaux slams Barry uses the new 12-frame throw animation, built exactly like D-044's Barry slams Peaux with the roles swapped: Peaux (slammer) S1–S5 `Assets/Art/Wrestlers/Peaux/peaux_bodyslam5_1..5.png`; Barry (slammed) R1–R7 `Assets/Art/Wrestlers/Barry/barry_bodyslammed5_1..7.png`, played R1…R7 then R6 held. Cell geometry, pivots and flip rules: `Assets/Art/Wrestlers/bodyslam_pb_cells.json` (copy of `Staging/Art/wrestler/gen-bodyslam-pb-pair-previews-5c/bodyslam_pb_cells_final.json`). S1/R1 and S2/R2 follow the designer's `Staging/Art/wrestler/Peaux Slams Barry Corrections.pdf` (Peaux's right forearm lifts Barry at the crotch, left palm flat on Barry's chest, Barry's gut reduced). Designer: "Yes, it's all approved."
  - **Throw:** Peaux's slam throws Barry the same way Barry's slam throws Peaux (D-044 rules: release, linear flight on x, ±variance, blocking, knocked down at the landing, slammer free at the landing). Designer: "Yes, throw."
  - **Starting point (timing):** S1 120, S2 120, S3 180, S4 100, S5 100 ms (R1–R5 the same), R6 100, R7 150, R6 held. Release at the start of S4/R4, 0.42 s; Peaux's bodyslam Startup (landing) 0.475 → **0.62 s**.
  - **Starting point (distances):** Peaux's `bodyslamThrowBySlammer` entry: `releaseOffsetPx` **159** (Barry's hands meet Peaux's at S4/R4, from Peaux's feet root), `landingDistancePx` **342** (1.59 × Peaux's 215 px height), `distanceVariance` 0.10, `releaseTime` 0.42. All tunable in the Inspector.
  - **Shared downed pose:** Barry's R6 becomes his downed sprite for every knockdown and knockout ("replace everywhere", as D-044 did for Peaux; D-044 kept Barry's old sprite only "until his own new art exists"). Barry's `downedMirrorShift` 3.0667 → 0, `fallLandingShift` 0 → 1.1. Crouch get-up (D-043) unchanged.
- **Why:** The approved Peaux-slams-Barry art is drawn as a throw, so the in-place slam no longer matches it; both wrestlers now share one bodyslam behaviour.
- **Supersedes:**
  - D-044: "Peaux slams Barry keeps its old animation, no throw" and "Barry keeps his current downed sprite". The v3 Peaux-slams-Barry sheets stay in the project, unused.
  - D-036 / D-044: Peaux's bodyslam impact 0.475 s → 0.62 s.
  - D-038: the 0.3 s Recovery after a bodyslam now applies only to a slammer whose throw entry is disabled (none in the shipped pair).
  - D-041: Barry's downed sprite at the end of his fall is R6.
- **Affects:** docs/COMBAT_SPEC.md, docs/ARCHITECTURE.md §6, docs/ACCEPTANCE_TESTS.md (T1.14, T2.6, T2.7), docs/ASSET_REGISTRY.csv (pb5/pb5b/pb5c rows), docs/CREDITS.md, CombatTuning.asset (+ tooltips), WrestlerAnimationBuilder, Peaux_Bodyslam / Barry_Bodyslammed / Barry_KnockedDown / Barry_Fall clips, FrameTiming, MoveAnimationTable, Barry prefab shifts, bodyslam tests.

---

## D-046: Theatrics pass ("over the top trailer park ass beating")

- **Date:** 2026-11-17
- **Decided by:** Designer (what each effect is for, blood style, knockback size, lighting mood, hitstop, slow-mo KO, impact flashes); numbers marked **Starting point** are set by the agents and tuned in playtest.
- **Decision:**
  - **Red flash:** every event that deals damage (strikes, stomps, bodyslam, truck/pool crash, run collision, any other damage source) and every knockdown or knockout flashes the receiving wrestler red. Juice owns it, driven by `CombatEvents`, the same for both wrestlers. Red flashes baked into some animation clips are replaced by this, so every hit looks the same.
  - **Blood:** chunky pixel-art droplets from particle systems (no generated art), heavy: small spray on a strike, bigger on strike 3 / stomps, big on bodyslam landings, truck/pool crashes and run collisions; sprays fly away from the attacker or obstacle. Drops that land leave **ground stains** on the dirt that build up over the match (Starting point: cap of about 80 stains; past the cap the oldest fade). Stains stay within the walkable ground and draw under the wrestlers. They are cleared when the Match scene unloads.
  - **Knockback about 2× (Combat, D-005 values in `CombatTuning`):** knockdown slide 0.5 → 1.0 units (D-041; duration may lengthen, Starting point 0.45 s). Non-knockdown hits get a short pushback away from the attacker (Starting point 0.15–0.2 units), small enough that the 3-hit combo still connects. Truck/pool crashes and run collisions push about 2× further than today. Bodyslam throw landing distance about 1.25× (D-044/D-045), keeping the art readable. All of it uses normal movement clamping.
  - **Screen shake:** scaled by impact (light on strikes, medium on strike 3 / stomps, heavy on slams, crashes and run collisions, heaviest on the knockout). Moves the child camera only (ARCHITECTURE §3.1).
  - **Hitstop:** a brief freeze of combat and wrestler animation on impact. Starting points: strike 1–2 about 50 ms, strike 3 / stomp about 90 ms, crash / run collision about 100 ms, slam landing about 120 ms. The Architect adds a pause hook in the match runner; Juice asks for it. Combat rules and timings are unchanged; only the clock pauses.
  - **Slow-mo knockout:** the knockout blow plays in slow motion (Starting point: 0.25× for about 1.0 s real time) with a camera punch-in, then the match ends as usual. This uses the Architect's hook, not `Time.timeScale` set by Juice.
  - **Impact flashes:** white pixel impact stars at the point of contact on hits, and dust clouds on slam landings, knockdown landings and crashes. No "POW" text.
  - **Lighting:** keep the approved golden-hour look (D-016) and enhance it: URP 2D lights (warm global light plus warm key light from the sky side and soft light pools), bloom, vignette and colour grading in the Match scene. Pixel-perfect rendering (D-013, D-017) must keep working.
- **Why:** Designer: "I want the theatrics of the game to feel like an 11 out of 10."
- **Supersedes:** D-041's 0.5-unit slide; D-044/D-045 landing distances (scaled about 1.25×); red flashes baked into animation clips.
- **Affects:** Combat (`CombatTuning` + asset, knockback/pushback), Match runner (hitstop/slow-mo hook), Juice (new effects scripts and prefabs), Match.unity (lighting, post-processing, Juice wiring), docs/COMBAT_SPEC.md, docs/ARCHITECTURE.md §3.1/§4.2/§4.6, docs/ACCEPTANCE_TESTS.md.

---

## Open Questions

Not yet decided. Agents must not resolve these on their own.

- **Q-001: Sticky Keys.** Resolved by D-019.
- **Q-002: Menu controls.** Resolved by D-019.
- **Q-003: When to start the Art Wrangler.** Resolved by D-008.
- **Q-004: Master copies of design documents.** The `.docx` files in docs/ are older than the `.md` versions. Recommended: treat the `.md` files as the source of truth.
- **Q-005: Craftpix license.** Resolved by D-011.
- **Q-006: Irish Whip direction under Focus.** Resolved by D-039.
