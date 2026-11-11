# Rural Rumble

## Executive Summary

*Rural Rumble* is a retro-inspired, 2D arcade wrestling game set in the dirt lots and gravel driveways of a fictional Bible Belt trailer park. Inspired by classic wrestling games such as *WrestleFest* and *Mat Mania*, the game combines fast-paced, eight-directional movement with responsive strikes and grappling mechanics.

Players compete in a one-on-one match against a computer-controlled opponent, using punches, kicks, and wrestling moves to gain the upper hand and deplete their opponent's health. The objective is to score a knockout (KO), ending the match and triggering a victory screen.

![](images/gdd-01.png) ![](images/gdd-02.png)

## Design Pillars: What is Central to the Experience?

![](images/gdd-03.png)

**Arcade Action** - *Rural Rumble* prioritizes fast-paced, responsive controls and fluid combat over complex wrestling simulation. Movement, strikes, and grapples should feel immediate, intuitive, and satisfying, encouraging players to focus on the action rather than complicated inputs.

**Momentum Swings** - Matches emphasize the back-and-forth nature of professional wrestling, where momentum can quickly shift between opponents. Well-timed strikes and grapples allow players to establish control, while missed attacks and poor positioning leave openings for their opponent to fight back.

![](images/gdd-04.png)

**Backyard Spectacle** - Eccentric characters, an unconventional trailer-park setting, and exaggerated wrestling action give *Rural Rumble* its distinctive personality. Every punch, bodyslam, and Irish Whip should capture the humor and chaotic energy of an unsanctioned backyard wrestling match.

## Game Mechanics

### Core Gameplay

*Rural Rumble* is a single-player, one-on-one arcade wrestling game developed in Unity. Players begin at the Start screen, choose one of two available wrestlers, and enter a match against the computer-controlled opponent.

The match takes place in a small outdoor arena located in a trailer park in Duckville, Louisiana. Players navigate the arena using eight-directional movement, positioning themselves to deliver strikes or initiate grapples.

The player can execute a punch/kick combination, enter a standing lock-up, perform a bodyslam, or Irish Whip their opponent across the arena. Successful attacks reduce the opponent's health, while missed attacks and poor positioning provide opportunities for the opponent to retaliate.

The objective is to deplete the opponent's health and achieve a knockout (KO). A successful knockout ends the match and displays a victory screen. If the computer-controlled opponent knocks out the player first, the match ends in defeat.

### MVP Scope

The initial playable version of *Rural Rumble* will include:

- **Characters:** Two selectable wrestlers, with one controlled by the player and the other by a finite-state-machine opponent.
- **Arena:** One outdoor trailer-park wrestling area.
- **Movement:** eight-directional movement and 2 directional running.
- **Striking:** One punch/kick combination.
- **Grappling:** One standing lock-up, one bodyslam, and one Irish Whip.
- **Combat States:** Standing, attacking, grappling, running, knocked down, recovering, and knocked out.
- **Match Rules:** Health-based damage and knockout victory conditions.
- **Game Flow:** Start screen, character selection, match, and victory/defeat presentation.

**Stretch Goals:** Pinfall and three-count mechanics, additional moves, two additional wrestlers, character entrances, and tag-team matches.

### Controls

![](images/gdd-05.png)

| Input | Action | Behavior |
|---|---|---|
| Left Stick / D-Pad | Movement | Move in eight directions within the arena |
| A | Lock Up / Grapple | Initiate a standing lock-up when within range; press again to execute a bodyslam |
| B | Run / Irish Whip | Run when standing freely; whip the opponent when controlling a lock-up |
| X | Strike | Execute a punch/kick combination through successive button presses during combo window |
| Y | Pin (stretch goal) | Attempt a pin while positioned near a knocked-down opponent |

### Gameplay Balance & Pacing

- Matches are designed to be fast-paced, with a target duration of approximately 3–5 minutes. Combat should emphasize responsive movement, short attack sequences, and impactful wrestling moves rather than the slower pacing of simulation-based wrestling games.
- The computer-controlled opponent will use a finite-state machine to alternate between approaching the player, striking, attempting grapples, and recovering from attacks. Its aggression and defensive behavior may adjust according to match conditions to encourage momentum swings without giving the opponent an unfair advantage.
- Strikes will provide opportunities to inflict consistent damage, while successful grapples will deliver greater damage at the cost of requiring closer positioning and leaving the wrestler vulnerable if unsuccessful.
- The initial version will not include manual reversals or parries. These mechanics may be introduced in future versions.

### Inspector-Tunable Values

To support fast iteration during playtesting, the following gameplay values must be editable in the Unity Inspector without changing code or requesting changes from Claude. Changes should take effect during Play Mode, and both wrestlers and the computer-controlled opponent must read the same values so a single edit applies to the whole match. Current default values are recorded in docs/COMBAT_SPEC.md (Tuning Values).

**Animation**

| Value | Governs |
|---|---|
| Animation speed at each step of an animation | How long each individual frame of a wrestler animation is held, adjustable per frame rather than only per clip |

**Movement**

| Value | Governs |
|---|---|
| Walk speed — horizontal | Movement speed when walking left or right |
| Walk speed — vertical | Movement speed when walking up or down the arena |
| Walk speed — diagonal | Movement speed when walking diagonally |
| Run speed | Movement speed while running |

**Damage (health points removed)**

| Value | Governs |
|---|---|
| Strike 1 damage | First hit of the punch/kick combination |
| Strike 2 damage | Second hit of the combination |
| Strike 3 damage | Combination finisher |
| Bodyslam damage | Health removed from the slammed wrestler |
| Run-collision damage | Health removed from both wrestlers when a running wrestler runs into the opponent |
| Stomp damage | Health removed by a stomp on a knocked-down opponent |

Changing a strike's animation speed may move its contact frame away from the combat hit window, so animation and combat timing should be checked together when either is adjusted.

### Opponent Finite State Machine

The computer-controlled opponent will use a deterministic finite-state machine (FSM) to simulate basic wrestling behavior without requiring runtime AI or external API calls. The opponent will evaluate its distance from the player, current combat state, and available actions to determine whether to approach, strike, attempt a grapple, or recover.

**Primary AI States**

| State | Behavior |
|---|---|
| Idle / Evaluate | Evaluates the player's position, distance, and current combat availability before choosing an action. |
| Approach | Moves toward the player using eight-directional movement until reaching an appropriate attack or grapple range. |
| Attack | Executes the punch/kick combination when the player is within striking range. |
| Grapple | Attempts a standing lock-up and, if successful, performs a bodyslam or Irish Whip. |
| Stunned / Downed | Temporarily loses offensive control after receiving a significant attack or being knocked down. |
| Recover | Completes the current recovery period before returning to Idle / Evaluate. |
| Knockout | A terminal state triggered when health reaches zero, ending the match. |

The opponent will use fixed rules and configurable probabilities to select between valid actions. These decisions will be evaluated at appropriate combat intervals rather than every rendered frame, preventing rapid or contradictory state changes.

Both the player and computer opponent will share the same movement, combat, damage, and recovery systems. The FSM will provide the opponent's inputs but will not bypass the normal combat rules.

**Example Gameplay Scenario**

![](images/gdd-06.png)

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

## Character Designs

![](images/gdd-07.png)

**Name:** Barry “Hot Potato” Lankford

**Profile:** A retired long-haul trucker and owner of a small dirt racing track in Hoon, Louisiana, Barry "Hot Potato" Lankford is known for his stubborn temperament and questionable lifestyle choices. After spending years driving cross-country, Barry now divides his time between organizing dirt track races and intimidating anyone unfortunate enough to cause trouble near his trailer. His favorite pastime is beating up the neighborhood punks who refuse to pick up their cigarette butts.

![](images/gdd-08.png)

**Name:** Peaux Strange

**Profile:** A trailer-park goth with a troubled past, Peaux Strange was expelled from high school at sixteen and quickly found his calling in backyard wrestling. Years of roughhousing with his college football playing older brothers have given him an unusually high tolerance for punishment. With a preference for eyeliner over eye black, Peaux has little patience for the loudmouthed rednecks who dominate his neighborhood.

## Setting & World

![](images/gdd-09.png)

*Rural Rumble* takes place in a fictionalized region of northeastern Louisiana, centered around the neighboring communities of McRae and West McRae. The surrounding area is home to swamps, lakes, dirt racing tracks, trailer parks, roadside casinos, farms, and small rural towns.

The initial game takes place in Duckville, a rundown community on the outskirts of West McRae. Matches are held in an improvised outdoor wrestling area surrounded by trailers, gravel driveways, and other fixtures of rural Southern life.

Future versions may expand into neighboring communities such as Hoon, Rayboro, Moldon, and Sticksburg, each with its own characters and wrestling environments.

![](images/gdd-10.png)

## Tone & Aesthetics

*Rural Rumble* combines dark Southern humor with exaggerated, retro-inspired arcade visuals. The world draws inspiration from the eccentric characters and irreverent comedy of *Squidbillies*, *Trailer Park Boys*, and *Beavis and Butt-Head*, set against the gritty rural atmosphere of *True Detective* Season 1 and the outrageous personalities of *Eastbound & Down*.

The game will feature colorful, expressive 2D wrestler sprites and exaggerated combat animations inspired by classic arcade wrestling games. Environments will contrast the absurdity of the characters with rundown trailers, dirt lots, swampy landscapes, and a generally rough, lived-in Southern aesthetic.

## Narrative

The initial version of *Rural Rumble* will feature minimal narrative, focusing primarily on the rivalry between its two playable wrestlers. Barry "Hot Potato" Lankford and Peaux Strange live in the same trailer park but represent very different personalities and lifestyles. Their mutual dislike eventually escalates into a backyard wrestling match to settle their differences.

Character backgrounds and the setting provide context and humor without interrupting the arcade-style gameplay. Future versions may expand the game's narrative through additional wrestlers, rivalries, and locations.

## AI Architecture

**Systems Architect & Integration Agent** - Defines shared system architecture, coordinates specialist agents, and integrates their work into a functional, playable Unity game.

**Combat State & AI Agent** - Implements deterministic 8-directional combat physics, synchronized two-wrestler grapple mechanics, and the finite-state machine controlling opponent AI behavior.

**Unity Animation & Tooling Agent** - Automates Unity asset and animation setup, connecting wrestler sprites, prefabs, and synchronized animations to the combat system.

**UI & Match Flow Agent** - Builds the complete non-combat game loop, managing UI scenes, character selection data persistence, real-time match health HUD, and knockout victory routing.

**QA & Testing Agent** - Creates and runs automated Unity tests to validate wrestling mechanics, opponent behavior, menu transitions, and overall game stability.

**Art Wrangler Agent** — Sources, generates, prepares, and catalogs visual assets that match Rural Rumble's retro wrestling aesthetic, including character sprites, trailer-park environments, and UI artwork, while documenting asset origins and usage rights.

**Sound Wrangler Agent** — Sources, generates, prepares, and catalogs music and sound effects for wrestling actions, arena atmosphere, and menus, ensuring that audio assets support the game's tone and have documented usage rights.

**Juice Agent** — Implements visual and sensory feedback such as hitstop, camera shake, impact effects, and dust particles to make strikes, bodyslams, and knockouts feel more impactful without altering combat rules.

**Provenance Clerk Agent** — Maintains a record of human-created, AI-generated, and AI-assisted assets, code, and development decisions so that their origins can be tracked and temporary AI content can be identified for future replacement.

All agents operate during development rather than during gameplay. The agents collaborate to generate, configure, integrate, and test the Unity systems that create the player experience. For example, the Combat State & AI Agent implements the mechanics of a bodyslam, the Unity Animation & Tooling Agent connects those mechanics to the synchronized wrestler animations, and the QA & Testing Agent verifies that the move correctly inflicts damage and returns both wrestlers to valid gameplay states.

Additional specialist agents will support asset creation, sound design, gameplay polish, and development tracking. The Art Wrangler and Sound Wrangler will source or generate candidate assets and maintain records of their origins and permitted uses. The Unity Animation & Tooling Agent will integrate approved assets into the game, while the Juice Agent will enhance existing combat events with audiovisual feedback without changing their underlying mechanics.

The Provenance Clerk will consolidate contribution records and identify AI-generated placeholder content for potential replacement with human-created assets in future versions. The QA & Testing Agent will validate completed features through automated tests, while human playtesting will remain responsible for evaluating control responsiveness, combat feel, and overall presentation.

## Technical Strategy

**Development Environment**

*Rural Rumble* will be developed using Unity and C#, with Claude Code agents assisting in the creation of gameplay systems, animation configurations, UI, and automated tests. The project will target a standalone Windows PC build with Xbox-style controller support.

**Multi-Agent Development Workflow**

A Systems Architect & Integration Agent will coordinate the specialist agents, establish shared interfaces, and integrate completed work. Individual agents will be assigned specific systems and files to reduce conflicting changes. Claude will interact with the Unity project through code generation, supported Unity CLI functionality, official Unity Claude Code plugin, and custom Unity Editor scripts where appropriate.

Completed work will be integrated and tested incrementally. Human playtesting will be required to validate controller responsiveness, combat pacing, animation quality, and overall player experience.

**Token Budget and API Constraints**

Development will use a provisional budget of **1,300,000 combined input and output Claude tokens** across the five-week project, allocated among nine development agents and a contingency reserve.

The largest allocations will be assigned to the Combat State & AI Agent (320,000 tokens), Systems Architect & Integration Agent (230,000 tokens), and Unity Animation & Tooling Agent (180,000 tokens), reflecting the complexity of combat implementation, synchronized animations, and system integration.

The remaining allocations will support UI & Match Flow (110,000), QA & Testing (75,000), Art Wrangler (110,000), Sound Wrangler (55,000), Juice Agent (100,000), and Provenance Clerk (20,000). An additional 100,000 tokens will be reserved for unexpected debugging, rework, and integration challenges.

![](images/gdd-11.png)

Token consumption will be monitored by task and agent. To reduce unnecessary usage, specialist agents will receive focused assignments, reference only relevant documentation, and avoid repeatedly processing unrelated project files. Independent tasks may be delegated concurrently, while changes to shared Unity scenes, prefabs, and animation assets will be integrated sequentially.

The budget represents an initial planning allocation rather than a verified usage limit. Actual token consumption will depend on selected models, tool calls, development complexity, and required revisions. The allocation will be reevaluated after the initial combat and Unity integration milestones.

Claude's context-window limitations, usage quotas, API rate limits, and Unity CLI tool permissions may constrain automated development. If an Editor operation cannot be completed through available tooling, the responsible agent may generate a Unity Editor script or provide instructions for manual implementation.

All Claude usage will occur during development. The finished game will run locally, with deterministic opponent behavior and no runtime LLM API dependencies.

**Technical Constraints and Scope**

The primary development challenges are responsive eight-directional combat, synchronized two-wrestler grapple animations, and reliable integration between AI-generated Unity scripts and assets.

To limit complexity, the initial game will feature two wrestlers sharing the same core combat systems, one arena, a limited moveset, and a single computer-controlled opponent. Additional wrestlers, advanced reversals, pinfalls, and alternative match types will be deferred until the core gameplay is stable.

The computer-controlled opponent will use deterministic, locally executed finite-state-machine logic rather than real-time LLM API calls, eliminating runtime token costs and network latency during gameplay.
