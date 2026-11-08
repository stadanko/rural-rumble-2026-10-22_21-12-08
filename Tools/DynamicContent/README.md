# Rural Rumble: Dynamic Content Pipeline (Assignment #04)

A development-time pipeline that reads Rural Rumble's design docs, generates in-game text with Claude, has an independent Content Critic check it against those docs, revises what the Critic rejects (at most twice), and saves only approved text as game-ready JSON. It changes nothing in Unity: no scripts, scenes, or prefabs.

## What content did you generate?

Three files in `outputs/`. All three were generated and Critic-approved in the final live run (`reports/runs/run_2026-11-08T215010Z.json`), then reviewed and approved by the designer, who edited some of the wording:

| File | Where it goes in the game | Final text (excerpt) | Designer edits |
|---|---|---|---|
| `barry_character.json` | Character Select screen, beside Barry's portrait (STYLE_GUIDE "UI") | **Retired trucker. Active ass-kicker.** "Years of long-haul trucking left Barry with a dirt track in Hoon and a bad attitude. He spends his free time beating up punks who won't pick up their cigarette butts." | None |
| `peaux_character.json` | Character Select screen, beside Peaux's portrait | **Eyeliner on. Rednecks beware.** "Expelled at sixteen, Peaux turned to backyard wrestling. His football-playing brothers made him hard to hurt. He picked eyeliner over eye black, and Duckville's rednecks can choke on it." | One word ("picks" → "picked") |
| `victory_messages.json` | Victory screen, a flavor line under the fixed "Victory! {Winner} knocked out {Loser} in {timer}" line | 4 lines per wrestler, e.g. Barry: "You look like you been chasin' parked cars, son." Peaux: "Nice mustache, Barry. Shame about the nap on the gravel." | 5 of 8 lines rewritten (all four of Barry's, Peaux's first) |

The exact text each model call produced, before any designer edits, is preserved in `reports/critic_results.json`. The designer-edited files were checked again with the deterministic validator (fields, names, length limits, out-of-scope mechanics) and pass it. They were not sent back through the LLM Critic.

Each file is flat JSON that Unity's `JsonUtility` can read (arrays of objects, no dictionaries). Each also carries a `provenance` block: `origin` (`AI-generated` for Barry; `mixed` for the two files the designer edited, with a `human_edits` note), model, timestamp, revision count, the doc sections read with their SHA-256 hashes, and `review_status: "designer-approved (2026-11-08)"`.

**Rerunning `pipeline.py` overwrites these files** with fresh, unedited generations. Running it to check the pipeline works is fine. Afterwards, restore the approved versions with `git checkout -- Tools/DynamicContent/outputs`.

## Does the content sound like your game?

Mostly yes, because every fact comes from the docs. No lore is hard-coded in the Python. Each call receives 13 doc sections (see below), and the character names and ids are parsed from the GDD's `**Name:**` lines.

- **Barry**: the text uses the GDD's own details: retired long-haul trucker, dirt track in Hoon, stubborn, and beating up punks who won't pick up their cigarette butts. The Style Guide adds the mustache, spare tire, and cut-off jean shorts.
- **Peaux**: expelled at sixteen, backyard wrestling, toughened by his football-playing brothers, eyeliner over eye black, no patience for loudmouth rednecks.
- **Setting and tone**: Duckville, gravel, trailers, and driveways, written with the blunt, slightly mean humor that GDD "Tone & Aesthetics" asks for (Squidbillies, Trailer Park Boys).
- **Knockout gameplay**: every victory line fits a KO ("nap on the gravel", "a knuckle sandwich"). None mentions a pin, submission, or title.

Weak spots the Critic noted as minor (non-blocking): Peaux's text never says "trailer-park goth" outright and drops "older"/"college" from his brothers. The Critic also called Barry's generated "too hot to handle" line a stock phrase. The designer rewrote all four of Barry's victory lines and one of Peaux's in their own Southern voice: the generated lines were on-model but sometimes generic.

## What did the Critic Agent catch?

All findings below are real Critic output, quoted from the saved reports.

**Spontaneous catches in normal runs** (nothing was planted):

- Run 3, Barry, first draft: rejected because the description had **four sentences** where the brief allows two or three, and the last two were fragments ("Cut-off jean shorts. Zero patience."). It also noted that "Drove trucks" lost the long-haul detail. Revision 1 approved.
- Run 3, Peaux, first draft: rejected for being **228 characters, over the 220 limit**. The validator caught this, and so did the LLM Critic, separately. The validator also flagged that the text was more than 35% longer than Barry's approved text. Revision 1 approved.
- Run 1, Barry, first draft: rejected because the **tagline was 43 characters** (limit 40). Revision 1 approved.
- Run 1, Peaux: the Critic said the description **never mentions backyard wrestling** but rated it only "minor", so the text was approved anyway. I then changed `.claude/agents/content-critic.md` to make any unmet brief requirement blocking. Runs 2 and 3 used that stricter Critic; run 1 is kept for the record.

**Deliberate consistency test** (`consistency_check.py`, evidence in `reports/consistency_check.json`). The error was **planted on purpose** from a hand-written fixture. The Critic did not find it on its own.

1. Original: `"Barry defeated Peaux by submission to win the world championship."`, plus three plain filler lines.
2. Critic findings on revision attempt 0: **REJECTED, 5 blocking issues**.
   - Mechanics guard: submission victory (GDD MVP Scope: knockout victory conditions only); championship/title (none exist in the docs).
   - LLM Critic, which is not shown the guard's findings: *"Submission and championship/title are not in the docs. The line also contradicts the main Victory line, which says 'knocked out'."* It also flagged the line's sports-broadcast tone, and the three filler lines for having no character personality.
3. Revision 1: six new lines, e.g. Barry: "Pick up your cigarette butts, Peaux. Oh wait, you're busy napping."
4. Final validation: **APPROVED**, with no out-of-scope mechanics left; all 8 checks passed (`result: "PASS"`). The revision is kept only in the report, not in `outputs/`.

**Rejection after exhausted revisions** is proven by offline tests with a mocked model (`tests/test_pipeline.py`): after two failed revisions the output is marked `rejected` and written only to `outputs/rejected/`, with its unresolved issues. A further test checks that the deterministic guard rejects a submission line even when the mocked LLM Critic approves it. No live run has hit this path yet, so `outputs/rejected/` does not exist.

## How it works

```
docs (GDD, STYLE_GUIDE, DECISION_LOG) ─► Generator ─► Critic ─► approved? ─► outputs/<file>.json
                                            ▲            │ no (attempt < 3)
                                            └─ findings ─┘
                                                         │ no, 2 revisions used
                                                         ▼
                                              outputs/rejected/<file>.json
```

| File | Role |
|---|---|
| `pipeline.py` | Entry point and revision controller (initial draft + at most 2 revisions), exporter, run report |
| `docs_loader.py` | Pulls named sections from the docs; fails if a doc or heading is missing; hashes each file |
| `specs.py` | Which doc sections to read, the three output specs and briefs, UI length limits, JSON schemas |
| `generate.py` + `prompts/generator.md` | Content Generator: docs + brief (+ previous draft and Critic findings on revisions) → JSON |
| `critic.py` + `../../.claude/agents/content-critic.md` | Content Critic. The agent file's body is the Critic's system prompt, so the pipeline stage and the Claude Code `content-critic` agent share one definition |
| `validate.py` | Deterministic checks: fields, ids, GDD names, length limits, 2–4 lines per wrestler, duplicates, placeholders, and a guard against out-of-scope mechanics (submission, title, pinfall, tag team, other match types), each with a doc citation |
| `llm.py` | Model backend: `claude -p` (Claude Code CLI, headless) |
| `consistency_check.py` + `fixtures/` | The deliberate-inconsistency demonstration |

The Critic has two independent layers. The deterministic validator always runs. The LLM Critic runs in a separate, fresh session and reviews five areas: character accuracy, game mechanics, tone and style, UI fit, and output validity. Content is approved only if the LLM approves **and** neither layer reports a blocking issue. Every report records the revision attempt number, each issue's category, severity, location, quoted text, explanation, and recommended fix, plus the cost of each call.

**Docs read** (13 sections): GDD (Executive Summary, Design Pillars, MVP Scope, Core Gameplay, Character Designs, Setting & World, Tone & Aesthetics, Narrative), STYLE_GUIDE (Overall Game Style, Characters, UI), DECISION_LOG (D-003 screen flow, D-026 match start and end).

## Setup and commands

Requirements: Python 3.10+ (tested on 3.14.0) with no pip packages, and Claude Code CLI 2.1+ on `PATH`, logged in (tested on 2.1.319). No API key is used or stored: each call is `claude -p --tools "" --no-session-persistence --json-schema ... --model claude-sonnet-5-5`, run from a temp folder so the session sees only the pipeline's prompt.

Run from the repo root:

```
python Tools/DynamicContent/pipeline.py                        # generate all three (exit 0 = all approved)
python Tools/DynamicContent/pipeline.py --only victory_messages
python Tools/DynamicContent/consistency_check.py               # planted-error test (exit 0 = PASS)
python -m unittest discover -s Tools/DynamicContent/tests -v   # 15 offline tests, mocked model
```

Options: `--model <id>`, `--max-revisions 0|1|2` (default 2).

**Outputs:** `outputs/*.json` (approved only), `outputs/rejected/*.json` (rejected), `reports/critic_results.json` (latest run, every attempt and finding), `reports/runs/run_<timestamp>.json` (one archive per run), `reports/consistency_check.json`.

## Executed runs (2026-11-08)

| Run | Barry | Peaux | Victory | Cost |
|---|---|---|---|---|
| 1 (original Critic prompt) | approved after 1 revision | approved, first draft | approved, first draft | $0.35 |
| 2 | approved, first draft | approved, first draft | approved, first draft | $0.14 |
| 3 (current `outputs/`) | approved after 1 revision | approved after 1 revision | approved, first draft | $0.29 |
| Consistency check | n/a | n/a | rejected → approved after 1 revision | $0.11 |

All three runs completed with every output approved. The text differs from run to run because the model isn't deterministic. What repeats is the process and the checks, not the wording.

## Limitations

- **The LLM Critic can be lenient on small details.** In run 2 it approved Peaux text that said "two" brothers (the GDD gives no number) and logged that only as minor. The deterministic guard covers hard mechanics rules only. Tone and lore judgment is up to the model, which is why the designer reviewed and edited the final text.
- **Length limits are pipeline assumptions** (tagline ≤ 40, description 80–220, victory line ≤ 90 characters, Peaux within 35% of Barry's length). The docs give no character counts, so they may need adjusting once the screens are laid out.
- **Designer edits aren't protected from reruns:** the pipeline doesn't know which files a human edited, and rerunning it overwrites them (see above).
- **Live rejection after exhausted revisions** is tested only with a mocked model (see above).
- **Backend:** requires a logged-in Claude Code CLI. Direct Anthropic API calls are not implemented (no API key in this environment).
- **Not integrated in Unity yet:** the Character Select and Victory screens don't read these files. Wiring them up is a later UI & Match Flow task.
- The assignment brief describes four-directional movement, but the GDD specifies eight-directional. The pipeline follows the GDD. This doesn't affect the generated text.
