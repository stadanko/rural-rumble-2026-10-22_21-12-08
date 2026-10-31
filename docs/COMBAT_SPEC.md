# Rural Rumble Combat Spec

## State Definitions

- Note: Wrestler and Opponent are used to describe the entity performing the move and the entity that is the victim of the move. These terms are interchangeable between the Player and AI Opponent, as either can be a Wrestler or Opponent in these definitions.
- **Standing** - Wrestler is standing in the arena and not moving.
- **Walking** - Wrestler is walking in one of eight directions around the arena.
- **Running** - Wrestler is running in one of two directions(left or right) around the arena. One press of Run starts it; it continues until the wrestler hits an obstacle or the arena edge, gets hit, performs another action, or presses Run again (D-020, D-027). Hitting the truck or pool puts the runner in Struck with obstacle collision damage; the trailer/fence line and arena ends just end the run (D-027). Pressing Strike while running ends the run and throws Strike 1; Lock Up isn't allowed while running. A runner below 30 health after obstacle damage is knocked down; a runner passes over a downed opponent (D-030). Running into the opponent knocks both wrestlers down (run-collision damage, tunable, starts at 0) (D-029). A whipped wrestler has no control at all until 3 s into the whip, then may cancel with Run. The whipper regains normal control when the Irish Whip animation finishes. A strike on a whipped wrestler deals normal strike damage (D-028).
- **Striking** - Wrestler is striking or attempting to strike an opponent.
- **Struck** - Wrestler is being struck by an Opponent. Lasts 0.4 s (tunable); each new hit restarts it (D-024).
- **Lock Up** - Wrestler is triggering or attempting to trigger the Lock Up animation.
- **Locked Up** - Opponent has triggered a lock up animation on the Wrestler.
- **Grappling** - Wrestler triggers a Grapple move from the Lock Up animation. (Body Slam, Suplex, etc)
- **Grappled** - Opponent triggers a Grapple move on the Wrestler that was in a Locked Up state.
- **Knocked Down -** Wrestler is in a downed animation, to show they are laying on the ground. A timer for when the Wrestler can get up. Length of the timer is based on how much health they have lost.
- **Recovering** - Wrestler is on their feet but they have a low amount of health and a Recovering animation is triggered. Walking is slowed in this state and Running is not allowed.
- **Knocked Out** - Wrestler’s health bar has been depleted and the fight is over. The Wrestler not in a Knocked Out state is declared the winner.

## Lock Up/Grappling/Irish Whip Rules

- In this description, Wrestler A will be triggering the moves, Wrestler B will be receiving the moves.
- Process of a Lock Up/Grapple/Irish Whip
    - States where a Lock Up is allowed
        - Standing(Wrestler A and/or B)
        - Walking(Wrestler A and/or B)
        - Striking(Wrestler B only, Lock Up must occur before  Wrestler B’s Strike lands in Wrestler A’s hitbox)
        - Recovering(Wrestler A and B)
    - A Grapple can only be performed when Wrestler A is in a Lock Up state and Wrestler B is in a Locked Up state
    - Wrestler A performs a successful Lock Up on Wrestler B
        - Wrestler B enters a Locked Up state
        - Lock Up animation is triggered
        - Timer starts for Wrestler A to perform a Grapple on Wrestler B
    - Wrestler A presses the Grapple button
        - Grapple animation is triggered
        - Wrestler B enters a Knocked Down state
        - Damage is applied to Wrestler B
    - Wrestler A presses Irish Whip button
        - If Wrestler A is pressing a Left or Right Movement
            - Irish Whip animation is triggered in the direction Wrestler A is pressing.
        - Else if Wrestler A is pressing no direction
            - Irish Whip animation is triggered in the direction Wrestler B is facing.
        - Running animation is triggered for Wrestler B
            - If Wrestler B runs into an object or Wrestler A strikes them, they enter a Struck state/animation
                - Damage is applied to Wrestler B
                - Wrestler B may enter Knocked Down state/animation if their health is below the Recovering threshold
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

## Tuning Values

Every value below must be editable in the Unity editor's Inspector without changing code. Keep them together in one shared combat tuning asset (for example, a ScriptableObject) that both wrestlers and the CPU opponent read from, so a change applies to the whole match. Values should be adjustable during Play Mode so they can be tuned while testing.

Values marked *Set* were decided by the designer. Values marked *Starting point* are initial estimates based on classic arcade wrestling games and are expected to change during playtesting.

### Timing

| Value | Default | Status | Notes |
|---|---|---|---|
| Combo window | 0.5 s | Set | Time allowed after a strike to press Strike again and continue the combo |
| Lock-up timer | 2.0 s | Set | Time Wrestler A has to press Grapple or Irish Whip before the lock-up breaks |
| Struck duration | 0.4 s | Set | D-024 |
| Strike 1/2 wind-up, hit window, recovery | 0.21 / 0.1 / 0.15 s | Starting point | D-024; wind-up matches the art's contact frame |
| Strike 3 wind-up, hit window, recovery | 0.125 / 0.1 / 0.25 s | Starting point | D-024 |
| Whip no-control time | 3.0 s | Set | D-028; after this the whipped wrestler may cancel with Run |

### Health and Damage

| Value | Default | Status | Notes |
|---|---|---|---|
| Max health | 100 | Set | From the Damage section |
| Recovering threshold | 30 | Set | Below this, the Wrestler enters Recovering |
| Strike 1 damage | 3 | Starting point | First hit of the combo |
| Strike 2 damage | 4 | Starting point | Second hit of the combo |
| Strike 3 damage | 6 | Starting point | Combo finisher; 13 total for a full combo |
| Bodyslam damage | 12 | Starting point | Grapples hit harder than strikes but need close range |
| Irish Whip impact damage | 8 | Starting point | Whipped Wrestler runs into an arena object |
| Obstacle collision damage | 8 | Starting point | A running Wrestler runs into the truck or pool (D-027) |
| Stomp damage | 3 | Starting point | D-025 |
| Run-collision damage | 0 | Starting point | D-029; running into the opponent knocks both down |
| Walk speed horizontal / vertical / diagonal | 3.0 / 2.0 / 3.6 units/s | Set | D-029; three separate values |
| Stomp cooldown | 1.0 s | Set | D-025; from the end of a stomp until the next stomp is allowed |

### Recovery and Knockdown

| Value | Default | Status | Notes |
|---|---|---|---|
| Recovery rate | 1 health/s | Starting point | Only while standing still in Recovering |
| Recovery cap | 30 | Set | Recovery stops at this health value, so standing still returns a Wrestler to Standing but never to full health. Adjust separately from the Recovering threshold. |
| Minimum knockdown time | 1.0 s | Starting point | Knockdown length at full health |
| Maximum knockdown time | 3.0 s | Starting point | Knockdown length near 0 health; length scales between the two by health lost |
| Mash reduction per press | 8% | Starting point | Each Strike or Grapple press removes this share of the remaining knockdown time |
| Shortest possible knockdown | 0.5 s | Starting point | Mashing cannot shorten a knockdown below this |
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
