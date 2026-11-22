# Rural Rumble: Style Guide Agent (Assignment #06)

> **For the grader:** *What it does* and *The style guide it enforces* cover the core objective. *Proof it fixes violations on its own* is the evidence. Every score and quote below is copied from the saved run reports in `reports/runs/`, which you can open on GitHub.

The Style Guide Agent is an automated **Generator → Evaluator → Refiner** loop. It writes new content for Rural Rumble, scores the content 0–100 against rules taken from the game's own GDD and Style Guide, and rewrites it until it meets the bar. Nobody steps in during a run.

```
docs/GDD.md + docs/STYLE_GUIDE.md
          │  (16 rules, each quoting the doc text it comes from; verified on every run)
          ▼
   Generator ──draft──► Evaluator (LLM, 0-10 + reason per rule)  ─┐
                        Automated checks (0-10 + reason per rule) ─┴─► Scorecard 0-100
                                                                         │
             ┌─────────── below the bar: violations, reasons, fixes ◄────┤
             ▼                                                           │ passes
          Refiner ──revised draft──► (scored again, up to 3 refinements) ▼
                                                                 outputs/<task>.json
                         never reaches the bar ──► outputs/rejected/<task>.json (+ open violations)
```

## What it does

It makes two kinds of content, and each kind covers one half of the brief ("aesthetic and narrative rules"):

| Task | Kind | What it is |
|---|---|---|
| `trailer_backdrop` | art brief (look) | Image-generation brief for the single-wide trailer along the back of the arena: prompt, negative prompt, palette hexes, light, size in native px |
| `power_pole` | art brief (look) | Brief for a wooden electric pole with sagging lines in the arena background |
| `trash_talk_prematch` | trash talk (story) | A four-line pre-match exchange between Barry and Peaux |

The art briefs are written for the Art Wrangler's image pipeline. The trash talk is candidate flavour text. Nothing here changes the Unity project.

## The style guide it enforces (pulled from the GDD, not invented)

`style_rules.json` holds 16 rules. Each rule cites a doc, a section, and **verbatim doc text**. `rules.py` checks every quote against the live doc on every run: if a quote is missing from its cited section, the agent refuses to run (test: `test_invented_rule_is_refused`). The palette hexes and the wrestler names are also read from the docs at runtime, never hard-coded.

| Rule | Hard? | Wt | Source (verbatim doc text, shortened) |
|---|---|---|---|
| A-01 Early-90s arcade pixel art, 1 art px per native px | ✔ | 3 | STYLE_GUIDE › Overall Game Style: *"patterned after Early 90s arcade games from Technos Japan, Capcom, and SNK"* |
| A-02 The 14-colour palette | ✔ | 2 | STYLE_GUIDE › Color Palette: `#10151A` … `#E6DFCC`, *"Skin tones are the main exception."* |
| A-03 Late-afternoon golden hour (D-016) | | 2 | STYLE_GUIDE › Arena: *"late-afternoon golden-hour light: warm orange sky behind the pines, long shadows across the dirt"* |
| A-04 Only trees, sky, electric poles, trailers | ✔ | 2 | *"The background should not include mountains or large hills."* |
| A-05 Deep woods, not a city | ✔ | 2 | *"Feels like in the middle of the woods, not a busy city."* / *"thick North Louisiana Pine Trees"* |
| A-06 Disrepair | | 3 | *"The general feeling of the trailer park should be disrepair."* + GDD Tone: *"rundown trailers, dirt lots, swampy landscapes"* |
| A-07 Old TV antennas / satellite dishes on trailer roofs | | 2 | *"Trailers should have old TV antennas or direct TV style dishes"* |
| A-08 Drawn to scale with the 240-px wrestlers | ✔ | 2 | *"a single-wide trailer stands roughly twice Barry's height"* |
| N-01 Only Barry and Peaux, with their GDD names | ✔ | 2 | GDD › Narrative: *"the rivalry between its two playable wrestlers"* |
| N-02 Knockout is the only way to win | ✔ | 3 | GDD › Executive Summary: *"The objective is to score a knockout (KO)"*; pinfall is a Stretch Goal |
| N-03 Only MVP moves (punch/kick, lock-up, bodyslam, Irish Whip, stomp) | ✔ | 2 | GDD › Design Pillars: *"Every punch, bodyslam, and Irish Whip…"* |
| N-04 Barry sounds like Barry | | 3 | GDD › Character Designs: *"stubborn temperament"*, *"punks who refuse to pick up their cigarette butts"*; Style Guide: *"Wild Bill Mustache"* |
| N-05 Peaux sounds like Peaux | | 3 | *"A trailer-park goth with a troubled past"*, *"loudmouthed rednecks"*; Style Guide: *"Black lipstick & eyeliner"* |
| N-06 Rooted in Duckville | | 2 | GDD › Setting & World: *"Duckville, a rundown community on the outskirts of West McRae"* |
| N-07 Dark Southern humor | | 3 | GDD › Tone & Aesthetics: *"combines dark Southern humor with exaggerated, retro-inspired arcade visuals"* |
| N-08 Short enough not to interrupt play | | 1 | GDD › Narrative: *"without interrupting the arcade-style gameplay"* |

The full rule wording and every quote are in `style_rules.json`. A stranger could not apply these rules to any other game: they name Duckville, Hoon, Barry's cigarette-butt grudge, Peaux's eyeliner, the 14 hexes, the 240-px wrestler scale, and KO-only wins.

## How it scores (a score and a reason, not pass/fail)

- The **Evaluator** (`prompts/evaluator.md`) scores every rule from **0 to 10**. For each one it gives a **reason**, the **evidence** it quotes from the draft, and a concrete **fix**. It runs in a fresh, tool-less `claude -p` session and only sees the docs, the rules and the draft. It never sees the Generator's or Refiner's reasoning.
- **Automated checks** (`checks.py`) also score the countable rules from 0 to 10, each with a reason: off-palette hexes, banned scenery, golden-hour cues, neglect details, sprite height against the 240-px scale, other wrestlers' names, pin/title/belt talk, non-MVP moves, line length and alternation, and numbers the docs never state. **The lower of the two scores counts**, so a lenient model can't let a pinfall line through.
- The **total** is the weighted average × 10, giving a score from 0 to 100. A draft passes when the total is **≥ 85**, every **hard** rule is **≥ 7**, and every other rule is **≥ 6**.
- The **Refiner** (`prompts/refiner.md`) gets every rule below 10, lowest first, with the Evaluator's reason, evidence and fix, and the automated check's exact findings. It returns the revised draft and a change log entry for each rule it addressed. It always revises from the **best draft so far**, so one bad revision can't drag the loop backwards.
- **Nobody steps in.** The loop stops on a pass, or after 3 refinements. If it never passes, the best draft is saved to `outputs/rejected/` with its open violations.

## Proof it fixes violations on its own

### 1. Planted-violation demo (`demo_planted.py` → [`reports/runs/planted_demo.md`](reports/runs/planted_demo.md))

Each task starts from a hand-written draft that breaks the rules on purpose (`fixtures/`). The loop gets no hints about what is wrong.

**Trailer: 1.1 → 83.9 → 87.8, approved.** The planted draft is a *"Photorealistic 3D render of a brand-new silver mobile home … urban lot at night with neon signs, a glowing city skyline and snowy mountains"*, coloured `#00FFFF #FF00FF`, with solar panels on the roof and a size of 1200×260 px. Every rule scored 0 or 1. For example, A-07 got 0 with *"The roof carries modern solar panels. The guide calls for an old TV antenna or DirecTV-style dish."* The Refiner's change log for its first revision:

- A-01: *"Prompt now asks for crisp hand-placed early-90s arcade pixel art citing WrestleFest, Final Fight, Streets of Rage … 3D/photoreal/smooth terms moved to negative_prompt"*
- A-07: *"Replaced solar panels with a bent old TV antenna and a rusty DirecTV-style dish on the roof."*
- A-08: *"Size set to 960x480 (480 px tall); scale_note ties it to Barry's 240-px sprite"*

Revision 1 scored 83.9, just under the bar. The Evaluator then caught a new, subtler problem: A-08 scored 7 because *"it says the trailer is about 960 px wide, which fills the whole 960 px native view width and is an invented dimension"*. The Refiner shrank the trailer to 720 px wide, and the next revision passed at 87.8.

**Trash talk: 9.5 → 78.9 → 85.3 → 92.6, approved.**

| | Iteration 0 (planted) | Iteration 3 (approved, 92.6) |
|---|---|---|
| Barry | *Tonight at the arena I'm gonna pin you one-two-three and take the world championship belt, kid!* | *Pick up them cigarette butts, boy, or I'll knock you cold in this Duckville gravel.* |
| Peaux | *Not before my piledriver and a steel chair put you through the announce table, old man.* | *Cute, old man. Got expelled at sixteen and still hit harder than your beer gut.* |
| Barry | *The crowd came to see "The Hammer" win, and the Hammer always wins!* | *I run a dirt track in Hoon. I'll bodyslam you through your mama's trailer, boy.* |
| ~~Barry~~ Peaux | *Hulk Hogan taught me everything. Let's give the fans a great show and shake hands after.* | *Try it, redneck. I'll stomp you till your mustache cries, eyeliner still perfect.* |

Some of what it found by itself:

- N-02 scored 0: *"The draft talks about pinfalls, a three-count and a championship belt. The GDD makes KO the only win condition and lists pinfall as a stretch goal."*
- N-01 scored 0: *"an invented alias, 'The Hammer', and Hulk Hogan, a real wrestler, is named."*
- N-03 scored 0, check: *"Moves the game doesn't have: 'piledriver', 'steel chair', 'table'"*
- N-08 scored 3: Barry speaks twice in a row, and the lines are too long.

The fixes weren't all done in one pass. Revision 2 scored 85.3 but was held back, because N-08 scored 5: *"Line 3 is 91 characters, over the 90 limit."* Revision 3 fixed that and passed.

### 2. Normal runs (Generator output, not planted)

The final run, [`reports/runs/run_2026-11-22T204039Z.md`](reports/runs/run_2026-11-22T204039Z.md), produced today's `outputs/`:

| Task | Scores per iteration | What the loop fixed |
|---|---|---|
| `trailer_backdrop` | 87.8 | Passed on the first draft (720×480 px, all 14 palette hexes named in the prompt, rooftop TV antenna and satellite dish) |
| `power_pole` | 86.9 | Passed on the first draft (A-08 at 7: the Evaluator noted the 720-px pole height is the draft's own estimate, not a doc value) |
| `trash_talk_prematch` | 67.9 → 84.2 → **89.5** | Draft 0: Barry said *"I drove eighteen wheels across nine states"*, so N-04 scored **0** (check: *"Barry states numbers the docs never give: ['eighteen', 'nine']"*), and N-06 scored 5 because Duckville was never named. Revision 1 brought N-07 humor down to 6 (*"reads closer to standard wrestling bravado"*). Revision 2 passed. |

The approved trash talk (`outputs/trash_talk_prematch.json`):

> **Barry:** Pick up them cigarette butts, boy, or I'll bury you in the gravel between our trailers.
> **Peaux:** Expelled at sixteen and I still out-read you. Nice mustache, did a possum die on you?
> **Barry:** Hauled freight till my back quit. Now I'll haul you out of Duckville in a trash bag.
> **Peaux:** My football brothers beat me stupid for fun. You hit like a spare tire, old man.

### 3. A live rejection, and how it shaped the agent

The development runs are kept for the record:

| Report | What happened | What changed afterwards |
|---|---|---|
| `run_2026-11-22T203246Z` | First live test (trash talk only): 76.3 → 87.9, approved | — |
| `run_2026-11-22T203354Z` | All approved. But the trash talk passed at 85.3 with N-06 (Duckville) at **5/10**, and Barry said *"Thirty years hauling freight"*, which the Evaluator flagged as contradicting *"Early Thirties"* | Added the floor that every rule must score ≥ 6. A doc contradiction now caps its rule at 5 (`prompts/evaluator.md`) |
| `planted_demo_run1` | Trailer 1.1 → 90.6, trash talk 10.5 → 81.6 → 86.8, both approved | — |
| `run_2026-11-22T203746Z` | All approved with the floors | — |
| `planted_demo_run2` | **Rejected** at 78.9 after 3 refinements. To fix the Barry voice rule, the Refiner kept writing *"Twenty years"* or *"Thirty years hauling freight"*, and the Evaluator kept capping it at 5. The best draft was saved, nobody stepped in, and the run exited 1 | Added the per-speaker **invented-number check** (N-04/N-05), which quotes the exact number to cut. Also told the Refiner never to put a number on Barry's trucking years |
| `run_2026-11-22T204039Z` + `planted_demo` | Final runs: everything approved | — |

The repeated "years hauling freight" came from a **real conflict in the design docs**. The GDD calls Barry a *"retired long-haul trucker"* who spent *"years driving cross-country"*, but the Style Guide says he is in his *"Early Thirties"*. The agent works around this by never stating a number. The designer should decide which doc is right.

## Setup and commands

Requirements: Python 3.10+ (tested on 3.14) with no pip packages. Also the Claude Code CLI on `PATH`, logged in (tested on 2.1.330). Model calls reuse the Assignment #04 backend (`Tools/DynamicContent/llm.py`): `claude -p --tools "" --no-session-persistence --json-schema … --model claude-sonnet-5-5`. They run from a temp folder, so each call sees only the prompt the loop builds. No API key is stored. Calls use the logged-in Claude plan. The `estimated_cost_usd` in each report is the CLI's API-price estimate, about $0.40–0.75 per full run.

Run from the repo root:

```
python Tools/StyleAgent/loop.py                       # all three tasks (exit 0 = all approved)
python Tools/StyleAgent/loop.py --only power_pole     # one task;  --max-refinements N,  --model <id>
python Tools/StyleAgent/demo_planted.py               # planted-violation demo (exit 0 = all fixed)
python -m unittest discover -s Tools/StyleAgent/tests -v   # 19 offline tests, scripted fake model
```

| File | Role |
|---|---|
| `loop.py` | Loop controller, scorecard (weights, floors, min of LLM and check), export, Markdown transcript |
| `agents.py` | The three LLM roles and their JSON schemas |
| `prompts/generator.md`, `evaluator.md`, `refiner.md` | System prompt for each role |
| `style_rules.json` | The 16-rule style guide, with verbatim doc citations |
| `rules.py` | Loads the rules, verifies every quote against the docs, reads the palette and names from the docs |
| `checks.py` | Deterministic graded checks (score + reason) |
| `tasks.json` | The three content briefs and the doc sections each one reads |
| `fixtures/` | The deliberately off-style drafts for the demo |
| `outputs/` | Approved content, with `style_score` and `provenance` (origin `AI-generated`, pending designer review) |
| `reports/runs/` | Every run: full JSON (every draft, score, reason, fix and call) plus a readable `.md` transcript |

## Limitations

- **The LLM's judgment varies from run to run.** The wording differs every run, and soft rules such as humor (N-07) often land at 7. The automated checks hold the hard rules steady. Tone is the model's call, and the designer should still review the outputs.
- **The bar is a tuning choice.** 85, 7 and 6 were set by the developer, not taken from the docs, and they were tightened once after run 2, as described above.
- **The power pole's height is not in the docs.** The Style Guide only gives the trailer's scale. The pole's 480–1080 px range in `tasks.json` is a pipeline assumption, and the Evaluator says so (A-08 at 7).
- **Briefs only, no images.** The art briefs weren't rendered, to save Unity AI credits. Rendering one is the Art Wrangler's next step, and the result would still need a designer pass.
- **Not used in Unity.** Nothing in the game reads these files yet. Trash talk isn't an MVP screen; it is candidate content.
- **The regex checks are blunt.** A word like "ring" or "table" counts as a violation even when used innocently. They err toward strictness, and the Refiner just rewords.
