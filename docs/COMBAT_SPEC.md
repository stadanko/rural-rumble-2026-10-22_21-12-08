# Rural Rumble Combat Spec

## State Definitions

- Note: Wrestler and Opponent are used to describe the entity performing the move and the entity that is the victim of the move. These terms are interchangeable between the Player and AI Opponent, as either can be a Wrestler or Opponent in these definitions.
- **Standing** - Wrestler is standing in the arena and not moving.
- **Walking** - Wrestler is walking in one of eight directions around the arena.
- **Running** - Wrestler is running in one of two directions(left or right) around the arena. One press of Run starts it; it continues until the wrestler hits an obstacle or the arena edge, gets hit, performs another action, or presses Run again (D-020, D-027). Hitting the truck or pool puts the runner in Struck with obstacle collision damage; the trailer/fence line and arena ends just end the run (D-027). Pressing Strike while running ends the run and throws Strike 1; Lock Up isn't allowed while running. A runner below 30 health after obstacle damage is knocked down; a runner passes over a downed opponent (D-030). Running into the opponent knocks both wrestlers down (run-collision damage, tunable, starts at 0) (D-029). A whipped wrestler has no control at all until 3 s into the whip, then may cancel with Run (after that a whip run follows the normal run rules). The whipper regains normal control when the Irish Whip animation finishes. A strike on a whipped wrestler deals normal strike damage (D-028). A whip run that hits the truck or pool deals Irish Whip impact damage instead of obstacle collision damage; arena ends just end it (D-027).
- **Striking** - Wrestler is striking or attempting to strike an opponent.
- **Struck** - Wrestler is being struck by an Opponent. Lasts 0.4 s (tunable); each new hit restarts it (D-024). While Struck the wrestler is pushed back a short way on x (D-046, see "Pushback" under Knocked Down).
- **Lock Up** - Wrestler is triggering or attempting to trigger the Lock Up animation.
- **Locked Up** - Opponent has triggered a lock up animation on the Wrestler.
- **Grappling** - Wrestler triggers a Grapple move from the Lock Up animation. (Body Slam, Suplex, etc)
- **Grappled** - Opponent triggers a Grapple move on the Wrestler that was in a Locked Up state.
- **Knocked Down -** Wrestler is in a downed animation, to show they are laying on the ground. A timer for when the Wrestler can get up. Length of the timer is based on how much health they have lost.
- **Recovering** - Wrestler is on their feet but they have a low amount of health and a Recovering animation is triggered. Walking is slowed in this state and Running is not allowed.
- **Knocked Out** - Wrestler’s health bar has been depleted and the fight is over. The Wrestler not in a Knocked Out state is declared the winner.

## Focus (D-037)

- **Facing each other:** a wrestler who is free to act (Standing, Walking, Recovering) faces the opponent left/right every tick, whatever direction they move. Moving away means backing up while still facing the opponent. If the two are exactly vertically aligned, the last left/right facing is kept.
- **Stance:** if the opponent's feet are more than `focusBackStanceHeightPx` (default 50 px, 60 px = 1 unit) higher on screen, the wrestler uses the **back** stance; otherwise the **front** stance. Equal height: both front. This holds whichever side each wrestler is on.
- **Locked facing:** while Striking, Struck, Knocked Down / Knocked Out, or in a lock-up or bodyslam, facing is not changed by Focus. It snaps back to the opponent when the wrestler is free to act again.
- **Running:** Focus is off for the runner, who faces the run direction. A run starts in the left/right direction held when Run is pressed, else toward the opponent. A non-running wrestler keeps focusing on a running opponent.
- **Input:** movement input never turns a wrestler or chooses the stance. Strikes and lock-ups reach in the facing direction, so while free to act they always reach toward the opponent.

## Lock Up/Grappling/Irish Whip Rules

- In this description, Wrestler A will be triggering the moves, Wrestler B will be receiving the moves.
- Process of a Lock Up/Grapple/Irish Whip
    - States where a Lock Up is allowed
        - Standing(Wrestler A and/or B)
        - Walking(Wrestler A and/or B)
        - Striking(Wrestler B only, Lock Up must occur before  Wrestler B’s Strike lands in Wrestler A’s hitbox)
        - Recovering(Wrestler A and B)
    - If no valid Wrestler B is in range, the Lock Up whiffs: Wrestler A grabs at air for the Lock-up whiff time, rooted and able to be hit, then returns to Standing or Recovering (D-031)
    - A Grapple can only be performed when Wrestler A is in a Lock Up state and Wrestler B is in a Locked Up state
    - During a Lock Up, Wrestler B has no control, and Wrestler A's Strike button is ignored (D-031)
    - Wrestler A performs a successful Lock Up on Wrestler B
        - Wrestler B enters a Locked Up state
        - Lock Up animation is triggered
        - Timer starts for Wrestler A to perform a Grapple on Wrestler B
    - Wrestler A presses the Grapple button
        - Grapple animation is triggered
        - Wrestler B enters a Knocked Down state
        - Damage is applied to Wrestler B
        - **Bodyslam throw (D-044).** Whether the slam throws depends on the slammer (Wrestler A): **on for both Barry and Peaux** (Barry D-044, Peaux D-045).
            - **Without a throw** (a slammer whose throw is off or has no entry; not used by the shipped pair since D-045, kept as the fallback): neither wrestler moves. Damage lands at the slammer's impact time; B stays Grappled until the move's Recovery ends (0.4 s later), then B is Knocked Down and A returns to Standing or Recovering (D-033, D-036, D-038).
            - **With a throw** (Barry slams Peaux, Peaux slams Barry): B is thrown in the direction B faced in the lock-up. B faces A, so B is carried over A and lands on A's far side. The throw is along x only, at the lock-up depth. Neither wrestler has control during the slam.
                - Lift (from the press to the **release**, 0.42 s): neither root moves. A turns to face the throw direction partway through (presentation).
                - Flight (from the release to the **landing**, 0.62 s): B's root moves on x, linearly, from the release offset (Barry 178 px, Peaux 159 px from A's root along the throw; A's feet are at his root from S3 on) to the landing distance. B passes through A (no wrestler blocking during the slam).
                - **Distance:** the median is 1.59 × the slammer's standing height from A's feet to B's landed body centre: for Barry 382 px, for Peaux (215 px tall) 342 px (each slammer's feet are drawn at his root from S3 on). D-046 throws about 1.25× further: **Barry 477.5 px = 7.96 units root to root, Peaux 427.5 px = 7.13 units**. Each slam draws a distance uniformly within **±10%** of the median.
                - **Blocked:** if the landing spot is past an arena end, or the flight reaches the truck or pool, B stops at contact with normal movement clamping (like the D-041 knockback slide) and lands there. Reaching the truck or pool adds the obstacle collision damage (8) at the landing. An arena end adds nothing. (The throw stays at the lock-up depth, so it never reaches the back wall.)
                - **Landing** (the impact): bodyslam damage (12, plus 8 if the throw hit the truck or pool) to B. B enters **Knocked Down** at once (normal health-based length plus the D-043 get-up; no fall, no knockback slide) or Knocked Out at 0. A returns to Standing or Recovering at the same moment, facing the throw direction (toward B), and is free to act. The R7 bounce is art only.
    - Wrestler A presses Irish Whip button
        - If Wrestler A is pressing a Left or Right Movement (the left/right part of the stick or D-pad; up/down alone counts as no direction, D-039)
            - Irish Whip animation is triggered in the direction Wrestler A is pressing.
        - Else if Wrestler A is pressing no direction
            - Irish Whip animation is triggered in the direction Wrestler B is facing. Wrestler B faces Wrestler A in the lock-up, so Wrestler B runs past Wrestler A (D-039).
        - Wrestler A plays the four-frame Irish Whip, rooted, facing locked, and has no control; when it ends Wrestler A faces the whip direction and returns to Standing or Recovering (D-028, D-039)
        - Running animation is triggered for Wrestler B (a whipped run: no control for the whip no-control time, then the normal run rules, D-028)
            - Wrestler B faces the whip direction from the start. During Wrestler A's whip, Combat moves Wrestler B along the keyed whip path (Irish Whip choreography below), drawn in front of Wrestler A and passing through Wrestler A with no blocking or run-collision knockdown (D-039)
            - If Wrestler B runs into an object (truck or pool: Irish Whip impact damage) or Wrestler A strikes them (that strike's damage, D-028), they enter a Struck state/animation
                - Damage is applied to Wrestler B
                - Wrestler B may enter Knocked Down state/animation if their health is below the Recovering threshold
            - The arena ends and the back wall just end the run, also when the whip path itself reaches an arena end (a whip started near an end) (D-027)
    - Wrestler does not press the Grapple or Irish Whip button within the timed interval
        - Break Grapple animation is triggered
        - Both wrestlers return to a Standing or Recovering state based on level of health.
- In the future, we will implement a reversal system for Lock Ups and Grapples. But we will not be implementing this in this version of the game.

## Striking/Combo Rules

- In this description, Wrestler A will be triggering the moves, Wrestler B will be receiving the moves.
- States where a Strike is allowed
    - Standing(Wrestler A and/or B)
    - Walking(Wrestler A and/or B)
    - Running(Wrestler B only)
    - Striking(Wrestler A and/or B, The first Strike to land in a wrestler’s hitbox is the one that registers. They other wrestler’s Strike is canceled and they enter a Struck state)
    - Lock Up(Wrestler B only, Wrestler A’s Strike must land in Wrestler B’s hitbox before Lock Up occurs)
    - Recovering(Wrestler A and B)
    - Struck(Wrestler B only, so combos connect; D-024)
    - Knocked Down(Wrestler B only, Stomp only; D-025)
- Wrestlers can't move while Striking (D-024).
- The "below 30 health" knockdown check happens after the strike's damage is applied (D-029).
- **Stomp (D-025):** Strike pressed while standing over a Knocked Down opponent performs a Stomp instead of a combo strike. It deals Stomp damage, doesn't change the knockdown timer, and can knock out. No limit per knockdown, but after a stomp ends the wrestler can't stomp again for the Stomp cooldown (1.0 s, tunable).
    - *Presentation only (D-047):* each stomp that lands on a lying wrestler bounces him off the ground like the end of the bodyslam (the bounce frame, about 150 ms, then the lying pose again). It changes no state, timing, damage or knockdown length. A stomp during the get-up crouch shows no bounce (unless it knocks him out: then he bounces and stays down).
- Process of striking
    - Wrestler A pushes strike button once
        - Strike 1 animation is triggered
        - When Wrestler A’s strike makes contact with Wrestler B’s hit box
            - Wrestler B enters Struck state
            - Wrestler B struck animation is triggered
            - Damage is applied to Wrestler B
            - Wrestler B may enter Knocked Down state if their health is below the Recovering threshold
    - Wrestler A pushes strike button a second time within combo time window
        - Strike 2 animation is triggered
        - When Wrestler A’s strike makes contact with Wrestler B’s hit box
            - Wrestler B enters Struck state
            - Wrestler B struck animation is triggered
            - Damage is applied to Wrestler B
            - Wrestler B may enter Knocked Down state if their health is below the Recovering threshold
    - Wrestler A pushes strike button a third time within combo time window
        - Strike 3 animation is triggered
        - When Wrestler A’s strike makes contact with Wrestler B’s hit box
            - Wrestler B enters Struck state
            - Wrestler B struck animation is triggered
            - Damage is applied to Wrestler B
            - Wrestler enters Knocked Down state
    - Wrestler A pushes strike button a second/third time outside combo time window
        - Strike 1 animation is triggered
        - When Wrestler A’s strike makes contact with Wrestler B’s hit box
            - Wrestler B enters Struck state
            - Wrestler B struck animation is triggered
            - Damage is applied to Wrestler B
            - Wrestler B may enter Knocked Down state if their health is below the Recovering threshold

## Damage

- Each wrestler starts with a full health bar. A health bar represents 100/100 points.
    - Points will never be referenced on screen, it’s simply for calculations in the code.
    - The visual representation will be a health bar on the screen for each player.
    - Damage is applied when a successful Strike or Grapple is performed.
    - Damage per attack is adjustable in the Unity editor for game balancing. See Tuning Values.
    - When a Wrestler drops below 30 points of health (the Recovering threshold), the Wrestler will enter the Recovering state.
    - If the wrestler is able to stand still in the Recovering state, they can recover a certain amount of health points per second. See Tuning Values.
    - If both wrestlers reach 0 health on the same tick, the match is a draw (D-030).
    - When an attack takes a Wrestler to 0 Health, the wrestler enters the Knocked Out state. The Knocked Down animation is triggered and they do not get up. The fight is over and their opponent is declared the winner.

## Knocked Down

A Wrestler may enter a Knocked Down state based on the following conditions:

- A grapple move is performed on the Wrestler
- A successful three strike combo is performed on the Wrestler
- A strike is performed on the Wrestler when below 30 points of health.

When in a Knocked Down state

- At the end of the Knocked Down state, the Wrestler will return to a Standing state if they have 30 or more health points, or a Recovering state if below 30 health points
- A timer begins to determine the duration of the Knocked Down state.
- The lower the Wrestler's health, the longer the timer. It scales linearly with health lost, from Minimum knockdown time at full health to Maximum knockdown time near 0 (D-025).
- The wrestler can push the strike or grapple buttons repeatedly to help speed up the Knocked Down state timer by a percentage. The CPU mashes too, at a tunable rate (D-025).
- A Knocked Down wrestler can be hit only by a Stomp (D-025). There is no invulnerability after getting up.

Get-up phase (D-043)

- When the knockdown timer runs out, the wrestler does not stand at once: he gets up over the **Get-up time** (0.25 s, tunable), shown as a crouch. Every knockdown, including the bodyslam receiver's, is that much longer. After it he returns to Standing or Recovering as above.
- The get-up is still part of the Knocked Down state, and every Knocked Down rule still applies: no control, only a Stomp can hit him (same stomp rules), a Stomp can knock him out, facing stays locked, and the CPU treats him the same as any Knocked Down wrestler.
- Mashing affects only the knockdown timer; presses during the get-up do nothing, so mashing never shortens the get-up.
- The knockback slide (D-041) belongs to the knockdown timer: the get-up does not extend it.

Falling down and knockback slide (D-041)

- **Which knockdowns fall:** a strike knockdown (Strike 3, or health below 30 after the damage), an obstacle crash below 30, both wrestlers in a run collision, and a **knockout from standing** (health reaches 0 from a strike, an obstacle crash or a run collision while the wrestler is not already down). The bodyslam receiver does not fall (the bodyslam keeps its own animation), and a Stomp knockout on a wrestler who is already down does not fall again.
- **Fall side:** the fall goes away from whoever or whatever hit them, on the left/right axis (x):
    - Strike: away from the attacker.
    - Run collision: each wrestler away from the other.
    - Obstacle (truck or pool): away from the obstacle, the opposite of the run direction.
    - Exactly level on x (e.g. stacked vertically): the opposite of the way the wrestler faces (with Focus they face the opponent, so they fall away from them).
- **Slide:** during the fall the wrestler slides the knockdown slide distance (1.0 unit, D-046; was 0.5 under D-041) along that side over the knockdown slide duration (0.45 s, D-046; the fall animation's default is 0.35 s, so the last 0.1 s is a short skid on the downed sprite), linearly, starting the tick after the knockdown. The slide uses normal combat movement, so it stops at the arena ends, the trailer/fence line and the truck/pool (a slide into an obstacle is not an obstacle hit). It does not change the knockdown length, damage or any other timing; a knockdown shorter than the slide ends the slide with it.
- **Knockout slide:** a knockout from standing slides the same way. The match is over (frozen) from the knockout tick, but the knocked-out wrestler's slide still finishes with the same clamped movement; nothing else changes (no timer, state changes or events), and the winner stays frozen (ARCHITECTURE 4.2). A Stomp knockout while already down does not slide.
- **Stomp range:** the slide can carry the victim out of stomp range (2.0 units); stepping in to stomp after a knockdown is intended.

Pushback on hits that don't knock down (D-046)

- **Strike pushback:** a Strike 1 or Strike 2 that leaves the defender Struck pushes him the hit pushback distance (0.2 units) on x, away from the attacker, linearly over the pushback duration (0.1 s), starting the tick after the hit. Each hit restarts it. It uses normal combat movement (arena ends, trailer/fence line, truck/pool, wrestler blocking; a pushback into an obstacle is not an obstacle hit). The attacker does not move.
- **Combo stays connected:** each strike pushback is cut short where the defender would leave the attacker's Strike 3 reach (Strike 3 reach + hurt-box half width = 2.3 units feet to feet at the defaults), so a 3-hit combo that would connect without pushback still connects. From 1.9 units or closer both pushbacks happen in full.
- **Crash pushback:** a run (or whip run) into the truck or pool that leaves the runner Struck pushes him the crash pushback distance (0.4 units, 2× the strike pushback) away from the obstacle over the same duration. A crash that knocks down uses the knockdown slide instead.
- **Only while Struck:** if Struck ends before the pushback is done, the rest is dropped. A knockdown or knockout never has a pushback (it has the fall and slide). A Stomp only ever hits a downed wrestler, so it never pushes back. The bodyslam and Irish Whip have no pushback. Damage, Struck length and every other timing are unchanged.

## Tuning Values

Every value below must be editable in the Unity editor's Inspector without changing code. Keep them together in one shared combat tuning asset (for example, a ScriptableObject) that both wrestlers and the CPU opponent read from, so a change applies to the whole match. Values should be adjustable during Play Mode so they can be tuned while testing.

Values marked *Set* were decided by the designer. Values marked *Starting point* are initial estimates based on classic arcade wrestling games and are expected to change during playtesting.

### Timing

| Value | Default | Status | Notes |
|---|---|---|---|
| Combo window | 0.5 s | Set | Time allowed after a strike to press Strike again and continue the combo |
| Lock-up timer | 2.0 s | Set | Time Wrestler A has to press Grapple or Irish Whip before the lock-up breaks |
| Lock-up whiff time | 0.4 s | Starting point | D-031; a Lock Up press with no opponent in range |
| Struck duration | 0.4 s | Set | D-024 |
| Strike 1/2 wind-up, hit window, recovery | 0.21 / 0.1 / 0.15 s | Starting point | D-024; wind-up matches the art's contact frame |
| Strike 3 wind-up, hit window, recovery | 0.125 / 0.1 / 0.25 s | Starting point | D-024 |
| Whip no-control time | 3.0 s | Set | D-028; after this the whipped wrestler may cancel with Run |
| Bodyslam impact (landing) time, per slammer | Barry 0.62 s, Peaux 0.62 s | Starting point | D-036, D-044; `bodyslamStartupByInitiator`. With a throw this is the landing: damage, B Knocked Down and A free all happen here. Each matches its slammer's throw clip (S1–S5 120 / 120 / 180 / 100 / 100 ms; Peaux's was 0.475 until his throw art, 2026-11-17); the designer will retune |
| Bodyslam active / recovery after impact (no throw only) | 0.1 / 0.3 s | Starting point | D-038; `bodyslamTiming`; B stays Grappled this long after impact. Not used by a thrown slam, so unused by the shipped pair since D-045 (fallback for a disabled entry) |
| Bodyslam throw on, per slammer | Barry on, Peaux on | Set | D-044, D-045; `bodyslamThrowBySlammer[].throwEnabled`. A slammer with no entry does not throw |
| Bodyslam release time | 0.42 s (Barry), 0.42 s (Peaux) | Starting point | D-044, D-045; `releaseTime`; start of S4/R4; must not be later than the impact time |
| Bodyslam release offset | 178 px (Barry), 159 px (Peaux) | Starting point | D-044, D-045; `releaseOffsetPx`; receiver root from the slammer root along the throw at the release (R4's drawn spot past the slammer's feet, which are at his root from S3 on; Peaux's 159 = where Barry's hands meet Peaux's at S4/R4, measured from the art) |
| Bodyslam throw distance (median) | 477.5 px = 7.96 units (Barry), 427.5 px = 7.13 units (Peaux) | Starting point | D-046 (1.25×), D-044, D-045; `landingDistancePx`; root to root = 1.25 × 1.59 × the slammer's standing height (Barry 240 px, Peaux 215 px; the slammer's feet are at his root from S3 on). D-044/D-045 set 382 / 342 px |
| Bodyslam throw variance | ±10% (both) | Set | D-044, D-045; `distanceVariance` 0.10; uniform, drawn once per slam, applied to the root-to-root distance |
| Irish Whip frame times f1 / f2 / f3 / f4 | 0.12 / 0.10 / 0.12 / 0.33 s (Barry whipping), 0.12 / 0.10 / 0.12 / 0.25 s (Peaux whipping) | Starting point | D-039; per whipper (`CombatTuning.irishWhipByWhipper`). The whipper is rooted for all four; the runner is released at the end of f3 and the whipper is free when f4 ends |
| Irish Whip runner offsets (px along the whip direction from the whipper, f1 / f2 / f3 / f4) | Barry whipping: through −80 / 55 / 98 / 222, away 58 / 64 / 98 / 222; Peaux whipping: through −55 / 50 / 80 / 167, away 70 / 74 / 82 / 167 | Starting point | D-039; the approved art's staging. Each key is held for its whole frame; from the f4 key the runner runs at run speed. "Through" = the runner keeps the direction it faced in the lock-up and passes the whipper; "away" = the way the whipper faced |

### Health and Damage

| Value | Default | Status | Notes |
|---|---|---|---|
| Max health | 100 | Set | From the Damage section |
| Recovering threshold | 30 | Set | Below this, the Wrestler enters Recovering |
| Strike 1 damage | 3 | Starting point | First hit of the combo |
| Strike 2 damage | 4 | Starting point | Second hit of the combo |
| Strike 3 damage | 6 | Starting point | Combo finisher; 13 total for a full combo |
| Bodyslam damage | 12 | Starting point | Grapples hit harder than strikes but need close range. A throw that reaches the truck or pool adds obstacle collision damage at the landing (D-044) |
| Irish Whip impact damage | 8 | Starting point | Whipped Wrestler runs into an arena object (truck or pool; used for the whole whip run instead of obstacle collision damage) |
| Obstacle collision damage | 8 | Starting point | A running Wrestler runs into the truck or pool (D-027); also added when a bodyslam throw reaches the truck or pool (D-044) |
| Stomp damage | 3 | Starting point | D-025 |
| Run-collision damage | 0 | Starting point | D-029; running into the opponent knocks both down |
| Walk speed horizontal / vertical / diagonal | 3.0 / 2.0 / 3.6 units/s | Set | D-029; three separate values |
| Stomp cooldown | 1.0 s | Set | D-025; from the end of a stomp until the next stomp is allowed |

### Focus

| Value | Default | Status | Notes |
|---|---|---|---|
| Focus back-stance height | 50 px | Set | D-037 (was 100 px); opponent this much higher on screen (or more) puts the wrestler in the back stance |

### Recovery and Knockdown

| Value | Default | Status | Notes |
|---|---|---|---|
| Recovery rate | 1 health/s | Starting point | Only while standing still in Recovering |
| Recovery cap | 30 | Set | Recovery stops at this health value, so standing still returns a Wrestler to Standing but never to full health. Adjust separately from the Recovering threshold. |
| Minimum knockdown time | 1.0 s | Starting point | Knockdown length at full health |
| Maximum knockdown time | 3.0 s | Starting point | Knockdown length near 0 health; length scales between the two by health lost |
| Mash reduction per press | 8% | Starting point | Each Strike or Grapple press removes this share of the remaining knockdown time |
| Shortest possible knockdown | 0.5 s | Starting point | Mashing cannot shorten a knockdown below this |
| Knockdown slide distance | 1.0 unit | Starting point | D-046 (D-041 set 0.5); `knockdownSlideDistance`; how far a falling wrestler slides on x, away from what hit them; also covers truck/pool crashes and run collisions that knock down |
| Knockdown slide duration | 0.45 s | Starting point | D-046 (D-041 set 0.35); `knockdownSlideDuration`; linear; the fall animation's default is 0.35 s, so the slide ends with a 0.1 s skid on the downed sprite |
| Hit pushback distance | 0.2 units | Starting point | D-046; `hitPushbackDistance`; Strike 1 / 2 -> Struck, away from the attacker; cut short at the attacker's Strike 3 reach so the combo still connects |
| Crash pushback distance | 0.4 units | Starting point | D-046; `crashPushbackDistance`; truck/pool crash -> Struck, away from the obstacle; 2× the hit pushback |
| Pushback duration | 0.1 s | Starting point | D-046; `pushbackDuration`; linear; only while Struck |
| Get-up time | 0.25 s | Starting point | D-043; `getUpTime`; crouch at the end of every knockdown, after the knockdown timer runs out; still Knocked Down, not shortened by mashing |
| CPU mash rate | 3 presses/s | Starting point | D-025 |

## CPU AI

The computer-controlled opponent will use a deterministic finite-state machine (FSM) to simulate basic wrestling behavior without requiring runtime AI or external API calls. The opponent will evaluate its distance from the player, current combat state, and available actions to determine whether to approach, strike, attempt a grapple, or recover.

**Primary AI States**

| State | Behavior |
|---|---|
| Idle / Evaluate | Evaluates the player's position, distance, and current combat availability before choosing an action. |
| Approach | Moves toward the player using eight-directional movement until reaching an appropriate attack or grapple range. |
| Attack | Executes the punch/kick combination when the player is within striking range. |
| Lock Up/Grapple | Attempts a standing lock-up and, if successful, performs a grapple or Irish Whip. |
| Stunned / Knocked Down | Temporarily loses offensive control after receiving a significant attack or being knocked down. |
| Recover | Completes the current recovery period before returning to Idle / Evaluate. |
| Knockout | A terminal state triggered when health reaches zero, ending the match. |

The opponent will use fixed rules and configurable probabilities to select between valid actions. These decisions will be evaluated at appropriate combat intervals rather than every rendered frame, preventing rapid or contradictory state changes.

Both the player and computer opponent will share the same movement, combat, damage, and recovery systems. The FSM will provide the opponent's inputs but will not bypass the normal combat rules.

**Example Gameplay Scenario**

![](images/combat-spec-01.png)

Peaux Strange is controlled by the computer while the player controls Barry "Hot Potato" Lankford.

1. Peaux begins outside striking range and enters the Approach state, moving toward Barry.
2. When Peaux reaches striking distance, the FSM selects Attack and executes a jab.
3. Barry successfully strikes Peaux during an opening, interrupting Peaux's offense and temporarily putting him into a Stunned state.
4. Peaux completes his recovery and returns to Idle / Evaluate.
5. Peaux approaches Barry again, this time selecting Grapple when within range.
6. If the lock-up succeeds, Peaux executes a bodyslam, inflicting damage and putting Barry into a knockdown/recovery state.
7. Peaux returns to Idle / Evaluate and resumes the match based on Barry's position and combat state.

The player experiences a back-and-forth match in which positioning, successful attacks, and temporary vulnerability create opportunities for momentum to shift between wrestlers.

The initial AI will not perform manual reversals or complex defensive maneuvers. Difficulty adjustments may influence decision frequency and offensive aggression, but the opponent will remain subject to the same core combat restrictions as the player.
