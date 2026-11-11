#!/usr/bin/env python3
"""Goal-oriented coding agent for Rural Rumble (Assignment #5).

The agent works in five passes:

  1. READ THE DESIGN DOCS  - parse docs/*.md into sections and confirm that each goal in
                             features.json is actually required by the doc it cites.
  2. SCAN THE CODEBASE     - index Assets/ files, C# type names and the Build Settings scene list.
  3. GAP DETECTION         - run each goal's "done-when" checks: missing / needs_integration / done.
  4. PRIORITY SCORING      - hard gates (blocked dependency, missing assets, unsafe paths), then a
                             weighted sum of designer ratings (config.json weights).
  5. WRITE + ITERATE       - ask Claude (headless `claude -p`, same backend as Assignment 4) for the
                             top goal's files, stage them, let the designer approve each file, copy the
                             approved files into Assets/, re-scan, and stop when the goal is closed,
                             the per-run feature cap is reached, or no actionable gaps remain.

Usage (from the repo root or anywhere):
  python Tools/GoalAgent/goal_agent.py plan                 # passes 1-4 only, no writes
  python Tools/GoalAgent/goal_agent.py run                  # full loop with interactive per-file approval
  python Tools/GoalAgent/goal_agent.py run --stage-only     # generate + stage, approve later with `apply`
  python Tools/GoalAgent/goal_agent.py apply start_screen --approve all
  python Tools/GoalAgent/goal_agent.py apply start_screen --approve Assets/UI/StartScreen.uss --reject Assets/UI/StartScreen.uxml

Python 3.10+ standard library only. Generation needs the Claude Code CLI on PATH (logged in).
"""
from __future__ import annotations

import argparse
import fnmatch
import json
import re
import shutil
import sys
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
STAGING = HERE / "staging"
RUNS = HERE / "runs"

# Reuse the Assignment 4 model backend (headless Claude Code CLI, no API key).
sys.path.insert(0, str(HERE.parent / "DynamicContent"))

STATUS_MISSING = "missing"
STATUS_NEEDS_INTEGRATION = "needs_integration"
STATUS_DONE = "done"
STATUS_NOT_REQUIRED = "not_required"
SATISFIED = (STATUS_DONE, STATUS_NEEDS_INTEGRATION)


# =============================================================== config

def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_config(here: Path = HERE) -> dict:
    return load_json(here / "config.json")


def load_features(here: Path = HERE) -> list[dict]:
    return load_json(here / "features.json")["features"]


# =============================================================== 1. read the design docs

_HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*$")


def parse_sections(markdown: str) -> dict[str, str]:
    """Map lower-cased heading text -> body. A body runs until the next heading of the same or
    higher level, so a section includes its subsections. Headings inside code fences are ignored."""
    lines = markdown.splitlines()
    heads = []
    in_fence = False
    for i, line in enumerate(lines):
        if line.lstrip().startswith("```"):
            in_fence = not in_fence
            continue
        m = None if in_fence else _HEADING.match(line)
        if m:
            heads.append((i, len(m.group(1)), m.group(2)))
    sections: dict[str, str] = {}
    for k, (i, level, title) in enumerate(heads):
        end = len(lines)
        for j, other_level, _ in heads[k + 1:]:
            if other_level <= level:
                end = j
                break
        sections.setdefault(title.lower(), "\n".join(lines[i + 1:end]).strip())
    return sections


class DocLibrary:
    """Lazy, cached access to design-doc sections."""

    def __init__(self, repo: Path = REPO):
        self.repo = repo
        self._cache: dict[str, dict[str, str]] = {}

    def sections(self, doc: str) -> dict[str, str]:
        if doc not in self._cache:
            path = self.repo / doc
            self._cache[doc] = parse_sections(path.read_text(encoding="utf-8")) if path.exists() else {}
        return self._cache[doc]

    def section(self, doc: str, title: str) -> str | None:
        return self.sections(doc).get(title.lower())


def check_requirement(feature: dict, docs: DocLibrary) -> tuple[bool, str]:
    """Is this goal actually required by the design docs? Returns (required, evidence)."""
    req = feature["requirement"]
    if "designer" in req:
        return True, f"designer: {req['designer']}"
    body = docs.section(req["doc"], req["section"])
    if body is None:
        return False, f"section '{req['section']}' not found in {req['doc']}"
    needle = req.get("match", "").lower()
    if not needle:
        return True, f"{req['doc']} > {req['section']}"
    for line in body.splitlines():
        if needle in line.lower():
            quote = line.strip().lstrip("-* ").strip()
            return True, f"{req['doc']} > {req['section']}: \"{quote[:160]}\""
    return False, f"'{req['match']}' not mentioned in {req['doc']} > {req['section']}"


# =============================================================== 2. scan the codebase

_TYPE_DECL = re.compile(r"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)")
_BUILD_SCENE = re.compile(r"-\s*enabled:\s*1\s*\n\s*path:\s*(\S+)")


@dataclass
class CodebaseScan:
    files: set[str] = field(default_factory=set)            # repo-relative posix paths under Assets/
    types: dict[str, str] = field(default_factory=dict)     # C# type name -> file
    build_scenes: list[str] = field(default_factory=list)   # enabled Build Settings scenes, in order

    def has_file(self, rel: str) -> bool:
        return rel in self.files

    def dir_has_content(self, rel_dir: str) -> bool:
        prefix = rel_dir.rstrip("/") + "/"
        return any(f.startswith(prefix) for f in self.files)


def scan_codebase(repo: Path = REPO) -> CodebaseScan:
    scan = CodebaseScan()
    assets = repo / "Assets"
    if assets.exists():
        for path in assets.rglob("*"):
            if not path.is_file() or path.suffix == ".meta":
                continue
            rel = path.relative_to(repo).as_posix()
            scan.files.add(rel)
            if path.suffix == ".cs":
                text = path.read_text(encoding="utf-8", errors="replace")
                for name in _TYPE_DECL.findall(text):
                    scan.types.setdefault(name, rel)
    build = repo / "ProjectSettings" / "EditorBuildSettings.asset"
    if build.exists():
        scan.build_scenes = _BUILD_SCENE.findall(build.read_text(encoding="utf-8"))
    return scan


# =============================================================== 3. gap detection

def eval_check(check: str, scan: CodebaseScan) -> bool:
    kind, _, arg = check.partition(":")
    if kind == "file":
        return scan.has_file(arg)
    if kind == "class":
        return arg in scan.types
    if kind == "scene_in_build":
        return arg in scan.build_scenes
    raise ValueError(f"unknown check kind '{kind}' in '{check}'")


@dataclass
class GoalStatus:
    feature: dict
    required: bool
    evidence: str
    status: str
    failed_code: list[str]
    failed_integration: list[str]

    @property
    def id(self) -> str:
        return self.feature["id"]


def detect_gaps(features: list[dict], scan: CodebaseScan, docs: DocLibrary) -> list[GoalStatus]:
    """Compare every goal's done-when checks against the scan.
    missing           - code checks fail: the agent can build it.
    needs_integration - code exists, but scene / Build Settings / asmdef wiring (Architect's job) doesn't.
    done              - every check passes."""
    out = []
    for f in features:
        required, evidence = check_requirement(f, docs)
        failed_code = [c for c in f["code_checks"] if not eval_check(c, scan)]
        failed_int = [c for c in f.get("integration_checks", []) if not eval_check(c, scan)]
        if not required:
            status = STATUS_NOT_REQUIRED
        elif failed_code:
            status = STATUS_MISSING
        elif failed_int:
            status = STATUS_NEEDS_INTEGRATION
        else:
            status = STATUS_DONE
        out.append(GoalStatus(f, required, evidence, status, failed_code, failed_int))
    return out


# =============================================================== write policy

def check_write_path(rel: str, policy: dict, repo: Path = REPO, must_be_new: bool = True) -> tuple[bool, str]:
    """May the agent write this file? Forbidden patterns always win over allowed prefixes."""
    rel = rel.replace("\\", "/").strip()
    if not rel or rel.startswith("/") or re.match(r"^[A-Za-z]:", rel) or ".." in rel.split("/"):
        return False, "not a clean repo-relative path"
    for pattern in policy["forbidden_patterns"]:
        if fnmatch.fnmatchcase(rel, pattern):
            return False, f"forbidden by '{pattern}'"
    if not any(rel.startswith(p) for p in policy["allowed_prefixes"]):
        return False, "outside the allowed folders"
    if must_be_new and (repo / rel).exists():
        return False, "file already exists (the agent never overwrites)"
    return True, "ok"


def touches_forbidden(rel_pattern: str, policy: dict) -> str | None:
    probe = rel_pattern.replace("**", "x/y.cs").replace("*", "x")
    for pattern in policy["forbidden_patterns"]:
        if fnmatch.fnmatchcase(rel_pattern, pattern) or fnmatch.fnmatchcase(probe, pattern):
            return pattern
    return None


# =============================================================== 4. gates + priority scoring

def gate(goal: GoalStatus, by_id: dict[str, GoalStatus], scan: CodebaseScan, policy: dict) -> str | None:
    """Hard rules checked before any scoring. Returns why the goal can't be built now, or None."""
    f = goal.feature
    for dep in f.get("depends_on", []):
        dep_status = by_id.get(dep)
        if dep_status is None or dep_status.status not in SATISFIED:
            return f"blocked: needs '{dep}' first"
    for asset_dir in f.get("requires_assets", []):
        if not scan.dir_has_content(asset_dir):
            return f"blocked: no assets in {asset_dir}/ yet"
    for touched in f.get("touches", []):
        hit = touches_forbidden(touched, policy)
        if hit:
            return f"unsafe: would modify protected '{touched}'"
    if not f.get("outputs"):
        return "unsafe: no files the agent is allowed to create"
    for out in f["outputs"]:
        ok, reason = check_write_path(out, policy, must_be_new=False)
        if not ok:
            return f"unsafe: planned file {out} is {reason}"
    return None


def priority_score(feature: dict, weights: dict, gate_reason: str | None = None) -> tuple[float, dict]:
    """Score = 0 if gated, otherwise sum(weight[k] * rating[k]).
    Ratings are the designer's 0-5 estimates in features.json; weights live in config.json
    (effort has a negative weight, so bigger jobs rank lower)."""
    if gate_reason:
        return 0.0, {}
    ratings = feature["ratings"]
    breakdown = {k: weights[k] * ratings.get(k, 0) for k in weights}
    return float(sum(breakdown.values())), breakdown


@dataclass
class Ranked:
    goal: GoalStatus
    score: float
    breakdown: dict
    gate_reason: str | None
    order: int

    @property
    def actionable(self) -> bool:
        return self.gate_reason is None


def rank_gaps(statuses: list[GoalStatus], scan: CodebaseScan, config: dict) -> list[Ranked]:
    """Rank every goal still missing. Ungated first by score (ties: lower effort, then catalog order)."""
    by_id = {s.id: s for s in statuses}
    ranked = []
    for order, s in enumerate(statuses):
        if s.status != STATUS_MISSING:
            continue
        reason = gate(s, by_id, scan, config["write_policy"])
        score, breakdown = priority_score(s.feature, config["weights"], reason)
        ranked.append(Ranked(s, score, breakdown, reason, order))
    ranked.sort(key=lambda r: (not r.actionable, -r.score, r.goal.feature["ratings"].get("effort", 0), r.order))
    return ranked


# =============================================================== reporting

def print_plan(statuses: list[GoalStatus], ranked: list[Ranked]) -> None:
    print("\n== Gap detection ==")
    for s in statuses:
        mark = {STATUS_DONE: "done", STATUS_NEEDS_INTEGRATION: "needs integration",
                STATUS_MISSING: "MISSING", STATUS_NOT_REQUIRED: "not required"}[s.status]
        print(f"  {s.id:<20} {mark:<18} {s.evidence}")
        if s.status == STATUS_NEEDS_INTEGRATION:
            print(f"  {'':<20} {'':<18} waiting on: {', '.join(s.failed_integration)}")
    print("\n== Priority ranking (gates, then weighted sum) ==")
    if not ranked:
        print("  (no missing goals)")
    for i, r in enumerate(ranked, 1):
        detail = r.gate_reason or "  ".join(f"{k}{v:+g}" for k, v in r.breakdown.items())
        print(f"  {i:>2}. {r.goal.id:<20} {r.score:>5g}   {detail}")


def plan(repo: Path = REPO, here: Path = HERE, quiet: bool = False):
    config, features = load_config(here), load_features(here)
    scan = scan_codebase(repo)
    statuses = detect_gaps(features, scan, DocLibrary(repo))
    ranked = rank_gaps(statuses, scan, config)
    if not quiet:
        print_plan(statuses, ranked)
    return config, scan, statuses, ranked


# =============================================================== 5. code-writing step

PROPOSAL_SCHEMA = {
    "type": "object",
    "properties": {
        "files": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "path": {"type": "string"},
                    "purpose": {"type": "string"},
                    "content": {"type": "string"},
                },
                "required": ["path", "purpose", "content"],
            },
        },
        "integration_notes": {"type": "array", "items": {"type": "string"}},
        "assumptions": {"type": "array", "items": {"type": "string"}},
    },
    "required": ["files", "integration_notes", "assumptions"],
}


def build_codegen_prompt(goal: GoalStatus, scan: CodebaseScan, config: dict, repo: Path = REPO) -> str:
    f = goal.feature
    docs = DocLibrary(repo)
    parts = [
        f"# Goal: {f['title']} ({f['id']})",
        f"Owner role: {f['owner_role']}",
        f"Why it is required: {goal.evidence}",
        f"What to build: {f['description']}",
        "## Files to create (use exactly these paths unless one is unnecessary)",
        *[f"- {p}" for p in f["outputs"]],
        "## The goal counts as built when these checks pass",
        *[f"- {c}" for c in f["code_checks"]],
        "## Write policy (any other path will be rejected)",
        "Allowed folders: " + ", ".join(config["write_policy"]["allowed_prefixes"]),
        "Never create: .asmdef, .meta, .unity, .prefab, .controller, .anim files, or anything under "
        "Combat, AI, Presentation, Core, Match, Animation, Art, Data.",
        "## Existing files in the agent's folders",
        *(sorted(p for p in scan.files
                 if any(p.startswith(a) for a in config["write_policy"]["allowed_prefixes"]))
          or ["(none yet)"]),
    ]
    for ref in f.get("context_sections", []):
        body = docs.section(ref["doc"], ref["section"])
        if body:
            parts += [f"## Design doc: {ref['doc']} > {ref['section']}", body]
    for rel in f.get("context_files", []):
        path = repo / rel
        if path.exists():
            parts += [f"## Existing code (read-only): {rel}", "```csharp", path.read_text(encoding="utf-8"), "```"]
    return "\n\n".join(parts)


def generate(goal: GoalStatus, scan: CodebaseScan, config: dict, repo: Path = REPO) -> tuple[dict, dict]:
    from llm import ClaudeCliBackend  # imported here so `plan` works without the CLI

    system = (HERE / "prompts" / "codegen.md").read_text(encoding="utf-8")
    user = build_codegen_prompt(goal, scan, config, repo)
    backend = ClaudeCliBackend(model=config["codegen"]["model"], timeout=config["codegen"]["timeout_seconds"])
    print(f"\n== Code-writing step: asking {backend.model} for '{goal.id}' ==")
    return backend.complete_json("codegen", system, user, PROPOSAL_SCHEMA)


def stage(goal: GoalStatus, proposal: dict, config: dict, repo: Path = REPO, staging: Path = STAGING) -> dict:
    """Write the proposal into staging/<goal>/ and record a policy verdict per file."""
    root = staging / goal.id
    if root.exists():
        shutil.rmtree(root)
    (root / "files").mkdir(parents=True)
    entries = []
    for item in proposal["files"]:
        rel = item["path"].replace("\\", "/").strip()
        ok, reason = check_write_path(rel, config["write_policy"], repo)
        if ok:
            dest = root / "files" / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(item["content"], encoding="utf-8", newline="\n")
        entries.append({"path": rel, "purpose": item["purpose"], "lines": item["content"].count("\n") + 1,
                        "status": "pending" if ok else "rejected_by_policy", "policy": reason})
    manifest = {"goal": goal.id, "staged_at": now(), "files": entries,
                "integration_notes": proposal.get("integration_notes", []),
                "assumptions": proposal.get("assumptions", [])}
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"\nStaged {sum(e['status'] == 'pending' for e in entries)} file(s) in {root.relative_to(repo)}")
    for e in entries:
        print(f"  [{e['status']}] {e['path']} ({e['lines']} lines) - {e['purpose']}"
              + ("" if e["status"] == "pending" else f"  <- {e['policy']}"))
    return manifest


def approve_interactively(goal_id: str, manifest: dict, staging: Path = STAGING) -> None:
    for e in manifest["files"]:
        if e["status"] != "pending":
            continue
        while True:
            answer = input(f"Approve {e['path']}? [y]es / [n]o / [v]iew: ").strip().lower()
            if answer == "v":
                print((staging / goal_id / "files" / e["path"]).read_text(encoding="utf-8"))
                continue
            if answer in ("y", "n"):
                e["status"] = "approved" if answer == "y" else "rejected"
                break


def apply_staged(goal_id: str, config: dict, repo: Path = REPO, staging: Path = STAGING,
                 approve: list[str] | None = None, reject: list[str] | None = None) -> dict:
    """Copy approved staged files into the project. Policy is re-checked at copy time."""
    root = staging / goal_id
    manifest = load_json(root / "manifest.json")
    approve, reject = approve or [], reject or []
    for e in manifest["files"]:
        if e["status"] != "pending":
            continue
        if e["path"] in reject:
            e["status"] = "rejected"
        elif "all" in approve or e["path"] in approve:
            e["status"] = "approved"
    for e in manifest["files"]:
        if e["status"] != "approved":
            continue
        ok, reason = check_write_path(e["path"], config["write_policy"], repo)
        if not ok:
            e["status"], e["policy"] = "rejected_by_policy", reason
            continue
        dest = repo / e["path"]
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / "files" / e["path"], dest)
        e["status"] = "applied"
        print(f"  wrote {e['path']}")
    manifest["applied_at"] = now()
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest


def verify_goal(goal_id: str, repo: Path = REPO, here: Path = HERE) -> GoalStatus:
    """Re-scan after a write and report whether the goal's gap closed."""
    print("\n== Re-scan after write ==")
    _, _, statuses, ranked = plan(repo, here)
    goal = next(s for s in statuses if s.id == goal_id)
    if goal.status == STATUS_MISSING:
        print(f"\n'{goal_id}' is still missing; failed checks: {goal.failed_code}")
    elif goal.status == STATUS_NEEDS_INTEGRATION:
        print(f"\n'{goal_id}' code is in place. Architect integration still needed: {goal.failed_integration}")
    else:
        print(f"\n'{goal_id}' is done.")
    return goal


# =============================================================== loop + CLI

def now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def write_run_log(entry: dict, runs: Path = RUNS) -> Path:
    runs.mkdir(exist_ok=True)
    path = runs / f"{datetime.now().strftime('%Y%m%d-%H%M%S')}-{entry['command']}.json"
    path.write_text(json.dumps(entry, indent=2), encoding="utf-8")
    return path


def ranking_snapshot(ranked: list[Ranked]) -> list[dict]:
    return [{"goal": r.goal.id, "score": r.score, "breakdown": r.breakdown, "gate": r.gate_reason} for r in ranked]


def cmd_run(args) -> int:
    log = {"command": "run", "started_at": now(), "iterations": []}
    exit_code = 0
    config = load_config()
    for _ in range(config["loop"]["max_features_per_run"]):
        config, scan, statuses, ranked = plan()
        actionable = [r for r in ranked if r.actionable]
        it = {"ranking": ranking_snapshot(ranked)}
        log["iterations"].append(it)
        if not actionable:
            msg = ("All required goals are built." if not ranked
                   else "Every remaining gap is gated (blocked or unsafe); nothing the agent may build.")
            print(f"\nStop: {msg}")
            it["stop"] = msg
            break
        top = actionable[0]
        print(f"\nSelected '{top.goal.id}' (score {top.score:g}).")
        it["selected"] = top.goal.id
        proposal, meta = generate(top.goal, scan, config)
        it["llm"] = meta
        manifest = stage(top.goal, proposal, config)
        it["staged"] = manifest
        if args.stage_only or not sys.stdin.isatty():
            print(f"\nReview staging/{top.goal.id}/ then run:\n"
                  f"  python Tools/GoalAgent/goal_agent.py apply {top.goal.id} --approve all")
            it["stop"] = "awaiting approval"
            break
        approve_interactively(top.goal.id, manifest)
        (STAGING / top.goal.id / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        it["applied"] = apply_staged(top.goal.id, config)
        after = verify_goal(top.goal.id)
        it["after"] = after.status
        if after.status == STATUS_MISSING:
            it["stop"] = "goal not closed"
            exit_code = 1
            break
    log["finished_at"] = now()
    print(f"\nRun log: {write_run_log(log).relative_to(REPO)}")
    return exit_code


def cmd_apply(args) -> int:
    config = load_config()
    split = lambda v: [p.strip() for p in v.split(",") if p.strip()] if v else []
    print(f"== Applying approved files for '{args.goal}' ==")
    manifest = apply_staged(args.goal, config, approve=split(args.approve), reject=split(args.reject))
    after = verify_goal(args.goal)
    _, _, _, ranked = plan(quiet=True)
    log = {"command": "apply", "goal": args.goal, "at": now(), "manifest": manifest,
           "after": after.status, "next_ranking": ranking_snapshot(ranked)}
    print(f"\nRun log: {write_run_log(log).relative_to(REPO)}")
    return 1 if after.status == STATUS_MISSING else 0


def main(argv=None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("plan", help="read docs, scan, detect gaps, score; no writes")
    p_run = sub.add_parser("run", help="plan, generate the top goal, approve, write, re-scan")
    p_run.add_argument("--stage-only", action="store_true", help="stop after staging; approve later with apply")
    p_apply = sub.add_parser("apply", help="copy approved staged files into the project and re-scan")
    p_apply.add_argument("goal")
    p_apply.add_argument("--approve", help="'all' or comma-separated paths")
    p_apply.add_argument("--reject", help="comma-separated paths")
    args = parser.parse_args(argv)
    if args.command == "plan":
        plan()
        return 0
    return cmd_run(args) if args.command == "run" else cmd_apply(args)


if __name__ == "__main__":
    sys.exit(main())
