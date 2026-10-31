# Rural Rumble — Claude Project Instructions

## Project Identity

Rural Rumble is a single-player, 2D arcade wrestling game
set in a fictional Louisiana trailer park.

Engine: Unity 6
Language: C#
Target: Windows PC
Input: Xbox-style controller and keyboard

The experience prioritizes:
1. Arcade Action — responsive, intuitive combat.
2. Dynamic Momentum Swings — control shifts between wrestlers.
3. Backyard Spectacle — exaggerated, humorous wrestling.

## MVP Scope

Required:
- Two wrestlers sharing a common combat framework.
- One trailer-park arena.
- Eight-directional movement and two-directional running.
- Punch/kick combination.
- Standing lock-up and bodyslam.
- Irish Whip.
- Damage, grounded recovery, and knockout.
- One finite-state-machine CPU opponent.
- Screen flow: Start Screen → Character Select → Match → Victory Screen
  (no game mode select in MVP).

Not MVP requirements:
- Pinfall / three-count system.
- Additional characters.
- Character entrances.
- Tag-team mode.
- Online multiplayer.

Do not implement stretch goals without approval.

## Source of Truth

Read only the documents relevant to the assigned task.

- docs/GDD.md — game vision and scope
- docs/CONTROLS.md — input mappings
- docs/COMBAT_SPEC.md — combat rules and states
- docs/STYLE_GUIDE.md — visual and audio direction
- docs/ARCHITECTURE.md — system interfaces and ownership
- docs/ACCEPTANCE_TESTS.md — completion criteria
- docs/ASSET_REGISTRY.csv — asset source records
- docs/DECISION_LOG.md — accepted design decisions

If documents conflict, stop and flag the conflict.
Do not silently invent or override gameplay requirements.

## Agent Organization

Systems Architect & Integration:
Owns architecture, integration, shared interfaces,
and build validation.

Combat State & AI:
Owns movement, attacks, grapples, damage,
and opponent finite-state-machine behavior.

Unity Animation & Tooling:
Owns sprite import setup, animation controllers,
prefab presentation, and reusable Editor utilities.

UI & Match Flow:
Owns menus, character selection, HUD,
scene progression, and result presentation.

QA & Testing:
Owns automated tests and regression reports.

Art Wrangler:
Finds, prepares, and catalogs visual assets.

Sound Wrangler:
Finds, prepares, and catalogs audio assets.

Juice Agent:
Owns non-gameplay combat feedback and effects.

Provenance Clerk:
Validates and maintains contribution, asset,
and decision records.

Detailed agent instructions live in .claude/agents/.

## Delegation Rules

- Plan the smallest testable implementation.
- Delegate only when specialization provides value.
- Avoid unnecessary parallel agents.
- Each agent must respect assigned file ownership.
- Do not modify unrelated files.
- Only the Systems Architect integrates shared scenes.
- Do not let multiple agents edit the same Unity
  scene, prefab, or Animator asset concurrently.
- Agents may use approved Unity CLI commands,
  official Unity Claude Code plugin skills,
  and custom Unity Editor utilities.
- Verify available tool capabilities before relying on them.

## Unity CLI Integration

- Unity CLI is the primary interface for automated
  Unity Editor operations.
- Use the official Unity Claude Code plugin to access
  relevant Unity-specific skills and workflows.
- Prefer supported Unity CLI commands over custom
  Editor scripts when existing commands meet the need.
- Use custom C# Editor scripts for repetitive or
  unsupported Editor operations.
- Run `unity status` to verify Editor connectivity
  before attempting Editor-dependent operations.
- Use `unity command` (including `unity command script eval`
  for running C#) when appropriate for supported Editor operations.
- Use `unity logs --console` to inspect Editor errors
  when supported by the installed CLI version.
- Check command availability and syntax before execution.
- Do not assume that every Editor action is supported.
- Report unsupported operations or connectivity failures.
- Never claim that an Editor modification succeeded
  without verifying the result.
- Avoid running conflicting Editor operations concurrently.
- Do not install additional Editor integrations or
  modify project-wide tooling without approval.

## Unity Rules

- Prefer Unity-supported APIs and project conventions.
- Preserve existing serialized assets and references.
- Avoid broad scene rewrites for small feature changes.
- Separate combat rules from animation presentation.
- All character input sources use shared combat logic.
- Do not place gameplay rules inside visual-effects code.
- Inspect Console errors after Editor changes.
- No external AI calls during runtime gameplay.

## Asset Rules

- Search and download only from permitted sources.
- Record source URL, creator, license, and acquisition date.
- Record human, AI, mixed, or unknown origin.
- Keep unverified assets out of production.
- Store newly sourced assets in a staging area.
- AI-generated placeholders must be marked for review.
- Never claim ownership or permissions without evidence.

## Git & Safety

- Commit source files, necessary assets, and documentation.
- Do not commit Library/, Temp/, Obj/, Logs/, or Builds/.
- Commit Unity .meta files.
- Use Unity Force Text serialization and Visible Meta Files.
- Create a checkpoint before risky asset changes.
- Do not overwrite user-authored work without approval.
- Never delete files or perform destructive Git operations
  without explicit authorization.
- Never commit secrets, keys, or access tokens.

## Definition of Done

A task is complete only when:
1. Acceptance criteria are satisfied.
2. Changed scripts compile without new errors.
3. Relevant automated tests pass, or failures are reported.
4. Modified Unity assets remain valid.
5. Documentation is updated if behavior changed.
6. Source/provenance information is recorded.
7. The agent reports changed files, tests, and limitations.
8. Human playtesting requirements are identified.

Compilation alone does not prove gameplay quality.

## Working Principles

- Prefer simple, playable implementations.
- Do not build generalized systems for future features.
- Keep tasks narrow enough to review.
- Do not repeatedly load the entire GDD or repository.
- Ask for approval before adding packages or major dependencies.
- Report blockers rather than hiding incomplete work.
- Preserve a playable main branch.
