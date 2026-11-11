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

---

## Open Questions

Not yet decided. Agents must not resolve these on their own.

- **Q-001: Sticky Keys.** Resolved by D-019.
- **Q-002: Menu controls.** Resolved by D-019.
- **Q-003: When to start the Art Wrangler.** Resolved by D-008.
- **Q-004: Master copies of design documents.** The `.docx` files in docs/ are older than the `.md` versions. Recommended: treat the `.md` files as the source of truth.
- **Q-005: Craftpix license.** Resolved by D-011.
