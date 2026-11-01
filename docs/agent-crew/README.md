# Rural Rumble Agent Crew

**Assignment #3: Build an Agent Crew**
**Game: Rural Rumble**, a single-player 2D arcade wrestling game set in a fictional Louisiana trailer
park, built in Unity 6 (C#) for Windows. Two wrestlers, Barry Lankford and Peaux Strange, fight with
strikes, lock-ups, bodyslams, and Irish Whips against a CPU opponent.

## What this crew produces

The crew turns a design request into **animated, playable wrestlers inside the Unity project**:

| Output | Made by | Where it lands |
|---|---|---|
| Wrestler sprite sheets built from the designer's concept art, with license and origin recorded for every file | Art Wrangler | `Assets/Art/Wrestlers/`, `docs/ASSET_REGISTRY.csv` |
| Combat rules: movement, strikes, lock-ups, damage, knockdown, and the CPU opponent's state machine | Combat State & AI | `Assets/Scripts/Combat/`, `Assets/Scripts/AI/` |
| Animator controllers, clips, and wrestler prefabs that play in sync with combat timing, plus the Editor tool that builds them | Unity Animation & Tooling | `Assets/Animation/`, `Assets/Prefabs/Wrestlers/`, `Assets/Scripts/Presentation/`, `Assets/Editor/Tooling/` |

Everything above is game-ready: it compiles in Unity 6 and runs in the match scene.

## The agents

Each agent is a Claude Code subagent defined in one Markdown file. The file is the agent's code: its
role, the folders it owns, what it reads, the rules it must follow, how it verifies its work, and what
it reports back.

| Agent | Definition | Owns | Reads | Hands off |
|---|---|---|---|---|
| **Art Wrangler** | [`art-wrangler.md`](../../.claude/agents/art-wrangler.md) | `Staging/Art/`, `Assets/Art/`, registry rows | Style Guide, concept art, Decision Log | Approved sprite sheets, plus a registry row giving each sheet's cell size |
| **Unity Animation & Tooling** | [`unity-animation-tooling.md`](../../.claude/agents/unity-animation-tooling.md) | `Assets/Animation/`, `Assets/Prefabs/Wrestlers/`, `Assets/Scripts/Presentation/`, `Assets/Editor/Tooling/` | Approved sheets, registry, COMBAT_SPEC state list, `CombatEvents` | Animator, clips, prefabs, `MoveAnimationTable` |
| **Combat State & AI** | [`combat-state-ai.md`](../../.claude/agents/combat-state-ai.md) | `Assets/Scripts/Combat/`, `Assets/Scripts/AI/`, tuning asset | COMBAT_SPEC, CONTROLS, GDD opponent FSM | `CombatEvents` (state changes, move timing, hits, paired moves) |

Supporting code the agents use:

- [`Tools/ArtPipeline/`](../../Tools/ArtPipeline/): Python scripts the Art Wrangler runs to crop, downscale, recolor, baseline-align, and pack frames into sheets.
- [`WrestlerAnimationBuilder.cs`](../../Assets/Editor/Tooling/WrestlerAnimationBuilder.cs): Unity Editor tool written by Animation & Tooling. It slices a wrestler's sheets and builds every clip, Animator state, and the prefab in one menu command.
- [`CombatEvents.cs`](../../Assets/Scripts/Combat/CombatEvents.cs): the contract between Combat and Animation.

## How the agents coordinate

See **[CREW_DIAGRAM.md](CREW_DIAGRAM.md)** for both diagrams.

1. **Hub and spoke.** The main Claude Code session is the orchestrator. It reads [`CLAUDE.md`](../../CLAUDE.md), splits a request into the smallest testable pieces, and briefs one agent at a time. Subagents cannot launch other agents (`disallowedTools: Agent`), so every handoff goes through the orchestrator.
2. **Contracts instead of conversation.** Agents pass work through files with fixed formats:
   - Art to Animation: approved sheets plus `ASSET_REGISTRY.csv`. Each row gives the sheet's cell size, so Animation slices it correctly without asking.
   - Combat to Animation: `CombatEvents`. Combat raises `MoveStarted` with the tuned `MoveTiming`, and `WrestlerView` scales each clip so its contact frame lands exactly when the hit becomes active.
3. **Strict ownership.** Each agent writes only its own folders, so two agents never edit the same asset. Animation listens to combat and never decides anything. Combat publishes events and never plays animation.
4. **Feedback loops.** If a frame faces the wrong way, Animation sends it back to the Art Wrangler instead of patching it. If animation timing can't match combat timing, Animation reports the mismatch instead of changing combat. If the spec leaves out a rule, Combat stops and asks instead of inventing one.
5. **Human gates.** No art reaches `Assets/Art/` until the designer approves it. AI-generated art is marked as a placeholder until reviewed.
6. **Verification.** Agents check their own work through the Unity CLI (`recompile_status`, console errors, EditMode tests) before reporting. A report must list changed files, test status, and what needs human playtesting.

## Evidence the crew runs

**Transcript:** [TRANSCRIPT_PEAUX.md](TRANSCRIPT_PEAUX.md) is a real session in which all three
agents ran. It includes every brief, report, and designer approval.

These commits were produced by the crew:

| Commit | What the crew did |
|---|---|
| `5c9c841` | Combat core and CPU opponent brain (Combat State & AI), plus Barry's strike animations (Animation & Tooling) |
| `ee29267` | Peaux from concept art to playable prefab. The Art Wrangler made his first base-sprite candidates. The orchestrator made the rest of his art after the designer stopped that run. Animation & Tooling built all his clips and his prefab. |
| `lock-ups` branch | Lock-up feature: rules and CPU behavior (Combat State & AI), lock-up sprites (Art Wrangler), and stand-in clips (Animation & Tooling). In progress. |

## How to run it

**Requirements:** Claude Code, Unity 6 with this project open, the Unity Claude Code plugin and
`unity` CLI connected to the Editor (`unity status`), Python 3 with Pillow, ffmpeg, and Unity AI
credits for art generation.

From the project root, start Claude Code and give the orchestrator a task. It picks the agents. You
can also name one directly:

```text
> Use the art-wrangler to make Barry's bodyslam poses from his base sprite.
> Use the unity-animation-tooling agent to build clips for the approved bodyslam sheet.
> Use the combat-state-ai agent to implement the bodyslam from COMBAT_SPEC.
```

## Limitations

- The orchestrator routes every handoff. Agents don't message each other directly.
- The designer has to approve art. That pause is deliberate, not a failure.
- AI-generated sprites are placeholders until the designer signs off, and the registry records this.
- The repo defines three more agents (Systems Architect, QA & Testing, UI & Match Flow). They support this crew but aren't part of this submission.
