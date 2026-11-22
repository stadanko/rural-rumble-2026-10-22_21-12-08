"""Rural Rumble Style Guide Agent: Generator -> Evaluator -> Refiner, with no human in the loop.

    python Tools/StyleAgent/loop.py                      # all tasks in tasks.json
    python Tools/StyleAgent/loop.py --only trash_talk_prematch
    python Tools/StyleAgent/demo_planted.py              # start from deliberately off-style drafts

Each draft is scored 0-100 (weighted 0-10 score per rule, each with a reason). Below the bar,
the Refiner fixes the violations the Evaluator and the automated checks named, and the new draft
is scored again, up to --max-refinements times. Approved content goes to outputs/; content that
never reaches the bar goes to outputs/rejected/ with its open violations.
"""
import argparse
import datetime as dt
import json
import sys
from pathlib import Path

import rules as rulebook_mod  # also puts Tools/DynamicContent on sys.path
import agents
import checks
from llm import DEFAULT_MODEL, ClaudeCliBackend  # Tools/DynamicContent/llm.py (shared with Assignment #04)

HERE = Path(__file__).resolve().parent
OUTPUTS = HERE / "outputs"
REPORTS = HERE / "reports"

THRESHOLD = 85      # total score (0-100) needed to approve
HARD_FLOOR = 7      # every hard rule must score at least this, whatever the total
SOFT_FLOOR = 6      # and no rule at all may sit below this (added after run 2 approved a 5/10 setting score)
MAX_REFINEMENTS = 3


def now():
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H%M%SZ")


def build_scorecard(task, rules, llm_eval, check_results, threshold=THRESHOLD, hard_floor=HARD_FLOOR,
                    soft_floor=SOFT_FLOOR):
    """Merge LLM scores and deterministic checks into one graded scorecard (the lower score counts)."""
    by_id = {}
    for s in (llm_eval or {}).get("scores", []):
        by_id.setdefault(s.get("rule_id"), s)
    out, wsum, total = [], 0, 0.0
    for rid in task["rules"]:
        r = rules[rid]
        llm_s, chk = by_id.get(rid), check_results.get(rid)
        candidates = [x["score"] for x in (llm_s, chk) if x]
        score = min(candidates) if candidates else 0
        reasons = []
        if llm_s:
            reasons.append(llm_s["reason"])
        if chk:
            reasons.append(f"[check] {chk['reason']}")
        if not candidates:
            reasons.append("Not scored by the Evaluator or any check; counted as 0.")
        out.append({"rule_id": rid, "title": r["title"], "domain": r["domain"], "weight": r["weight"],
                    "hard": r["hard"], "score": score, "reason": " ".join(reasons), "llm": llm_s, "check": chk})
        wsum += r["weight"]
        total += r["weight"] * score
    total = round(10 * total / wsum, 1)
    hard_fail = [x["rule_id"] for x in out if x["hard"] and x["score"] < hard_floor]
    soft_fail = [x["rule_id"] for x in out if not x["hard"] and x["score"] < soft_floor]
    passed = total >= threshold and not hard_fail and not soft_fail
    return {"total": total, "threshold": threshold, "hard_floor": hard_floor, "soft_floor": soft_floor,
            "passed": passed, "hard_failures": hard_fail, "floor_failures": soft_fail, "rules": out,
            "summary": (llm_eval or {}).get("summary", "")}


def evaluate(llm, task, docs, book, draft, rules, ctx, calls):
    """Score a draft. Retries the Evaluator once if it skips rules."""
    result = None
    for _ in range(2):
        result, meta = agents.evaluate(llm, task, docs, book, draft)
        calls.append(meta)
        scored = {s.get("rule_id") for s in result.get("scores", [])}
        if set(task["rules"]) <= scored:
            break
    return build_scorecard(task, rules, result, checks.run_checks(draft, task, rules, ctx))


def run_task(llm, task, rules, doc_sections, ctx, seed_draft=None, max_refinements=MAX_REFINEMENTS,
             log=print):
    pack = rulebook_mod.doc_pack(doc_sections, task["docs"])
    docs, book = pack.as_prompt(), rulebook_mod.rulebook_prompt(rules, task["rules"])
    calls, iterations = [], []

    if seed_draft is not None:
        draft, origin = seed_draft, "seed"
    else:
        draft, meta = agents.generate(llm, task, docs, book)
        calls.append(meta)
        origin = "generator"

    best = None
    for i in range(max_refinements + 1):
        card = evaluate(llm, task, docs, book, draft, rules, ctx, calls)
        it = {"iteration": i, "made_by": origin if i == 0 else "refiner", "draft": draft, "scorecard": card}
        iterations.append(it)
        low = ", ".join(f"{r['rule_id']} {r['score']}" for r in card["rules"] if r["score"] < 8) or "none"
        log(f"  [{task['id']}] iteration {i}: {card['total']}/100 {'PASS' if card['passed'] else 'below bar'}"
            f" (rules under 8: {low})")
        if best is None or (card["passed"], card["total"]) > (best["scorecard"]["passed"], best["scorecard"]["total"]):
            best = it
        if card["passed"] or i == max_refinements:
            break
        # Refine from the best draft so far, so one bad revision can't drag the loop backwards.
        base = best
        result, meta = agents.refine(llm, task, docs, book, base["draft"], base["scorecard"])
        calls.append(meta)
        it["refined_from_iteration"] = base["iteration"]
        it["refiner_changes"] = result.get("changes", [])
        draft = result["draft"]

    status = "approved" if best["scorecard"]["passed"] else "rejected"
    return {"task_id": task["id"], "kind": task["kind"], "brief": task["brief"], "status": status,
            "final_iteration": best["iteration"], "final_score": best["scorecard"]["total"],
            "score_trajectory": [x["scorecard"]["total"] for x in iterations],
            "final_draft": best["draft"], "iterations": iterations, "calls": calls,
            "docs_read": pack.manifest()}


def export(result, model, run_id):
    card = result["iterations"][result["final_iteration"]]["scorecard"]
    payload = {
        "id": result["task_id"], "kind": result["kind"], **result["final_draft"],
        "style_score": {"total": card["total"], "threshold": card["threshold"],
                        "rules": {r["rule_id"]: {"score": r["score"], "reason": r["reason"]} for r in card["rules"]}},
        "provenance": {"origin": "AI-generated", "generator": "Tools/StyleAgent (Assignment #06)", "model": model,
                       "run_id": run_id, "iterations": len(result["iterations"]),
                       "score_trajectory": result["score_trajectory"], "docs_read": result["docs_read"],
                       "review_status": "AI-generated placeholder; pending designer review"},
    }
    folder = OUTPUTS if result["status"] == "approved" else OUTPUTS / "rejected"
    if result["status"] == "rejected":
        payload["open_violations"] = [{"rule_id": r["rule_id"], "score": r["score"], "reason": r["reason"]}
                                      for r in card["rules"] if r["score"] < 8]
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / f"{result['task_id']}.json"
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return path


# ------------------------------------------------------------------ markdown transcript

def _draft_md(kind, draft):
    if kind == "trash_talk":
        return "\n".join(f"> **{l.get('speaker', '?').title()}:** {l.get('text', '')}  " for l in draft.get("lines", []))
    rows = [f"- **{k}:** {draft[k]}" if not isinstance(draft[k], (dict, list)) else f"- **{k}:** `{json.dumps(draft[k])}`"
            for k in ("subject", "prompt", "negative_prompt", "palette", "lighting", "details", "size_px",
                      "scale_note", "layer") if k in draft]
    return "\n".join(rows)


def _cell(text):
    return str(text).replace("|", "\\|").replace("\n", " ")


def transcript_md(run):
    out = [f"# Style Guide Agent run `{run['run_id']}`", "",
           f"Model `{run['model']}` · threshold {run['threshold']}/100 · every hard rule >= {run['hard_floor']}/10 · "
           f"every other rule >= {run.get('soft_floor', 'n/a')}/10 · "
           f"max {run['max_refinements']} refinements · no human input during the run.", ""]
    if run.get("note"):
        out += [run["note"], ""]
    out += ["| Task | Result | Score per iteration |", "|---|---|---|"]
    for t in run["tasks"]:
        out.append(f"| `{t['task_id']}` | **{t['status']}** | {' → '.join(str(s) for s in t['score_trajectory'])} |")
    for t in run["tasks"]:
        out += ["", f"## `{t['task_id']}` ({t['kind']})", "", f"*Brief:* {t['brief']}", ""]
        for it in t["iterations"]:
            c = it["scorecard"]
            verdict = "PASS" if c["passed"] else ("below bar"
                + (f"; hard rules under {c['hard_floor']}: {', '.join(c['hard_failures'])}" if c["hard_failures"] else "")
                + (f"; rules under {c.get('soft_floor')}: {', '.join(c.get('floor_failures', []))}" if c.get("floor_failures") else ""))
            out += [f"### Iteration {it['iteration']} (from {it['made_by']}): {c['total']}/100, {verdict}", "",
                    _draft_md(t["kind"], it["draft"]), "",
                    "| Rule | W | Score | Evaluator | Automated check |", "|---|---|---|---|---|"]
            for r in c["rules"]:
                name = f"{r['rule_id']}{' (hard)' if r['hard'] else ''} {r['title']}"
                llm_s = f"{r['llm']['score']}: {r['llm']['reason']}" if r["llm"] else ""
                chk = f"{r['check']['score']}: {r['check']['reason']}" if r["check"] else ""
                out.append(f"| {_cell(name)} | {r['weight']} | **{r['score']}** | {_cell(llm_s)} | {_cell(chk)} |")
            if c.get("summary"):
                out += ["", f"*Evaluator summary:* {c['summary']}"]
            if it.get("refiner_changes"):
                out += ["", f"**Refiner** (revising iteration {it['refined_from_iteration']}):"]
                out += [f"- {ch['rule_id']}: {ch['change']}" for ch in it["refiner_changes"]]
            out.append("")
    return "\n".join(out) + "\n"


# ------------------------------------------------------------------ entry point

def run(task_ids=None, seeds=None, model=DEFAULT_MODEL, max_refinements=MAX_REFINEMENTS, write_outputs=True,
        report_name=None, note=None, llm=None, log=print):
    rules = rulebook_mod.load_rules()
    tasks, doc_sections = rulebook_mod.load_tasks(rules)
    if task_ids:
        tasks = [t for t in tasks if t["id"] in task_ids]
    narrative = rulebook_mod.doc_pack(doc_sections, "narrative")
    ctx = {"palette": rulebook_mod.palette_from_docs(), "characters": narrative.characters(),
           "doc_numbers": checks.doc_numbers(narrative.as_prompt())}
    llm = llm or ClaudeCliBackend(model=model)
    run_id = now()
    log(f"Style Guide Agent run {run_id}: {len(tasks)} task(s), model {model}")
    results = []
    for task in tasks:
        res = run_task(llm, task, rules, doc_sections, ctx, (seeds or {}).get(task["id"]), max_refinements, log)
        if write_outputs:
            res["output_file"] = str(export(res, model, run_id).relative_to(HERE)).replace("\\", "/")
        results.append(res)
        log(f"  [{task['id']}] {res['status'].upper()} at {res['final_score']}/100 "
            f"after {len(res['iterations'])} scoring(s)")
    cost = round(sum((c.get("cost_usd") or 0) for r in results for c in r["calls"]), 4)
    report = {"run_id": run_id, "model": model, "threshold": THRESHOLD, "hard_floor": HARD_FLOOR, "soft_floor": SOFT_FLOOR,
              "max_refinements": max_refinements, "note": note, "estimated_cost_usd": cost, "tasks": results}
    name = report_name or f"run_{run_id}"
    (REPORTS / "runs").mkdir(parents=True, exist_ok=True)
    (REPORTS / "runs" / f"{name}.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (REPORTS / "runs" / f"{name}.md").write_text(transcript_md(report), encoding="utf-8")
    log(f"Report: reports/runs/{name}.md (estimated cost ${cost})")
    return report


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*", help="task ids to run (default: all)")
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--max-refinements", type=int, default=MAX_REFINEMENTS)
    args = ap.parse_args(argv)
    report = run(args.only, model=args.model, max_refinements=args.max_refinements)
    return 0 if all(t["status"] == "approved" for t in report["tasks"]) else 1


if __name__ == "__main__":
    sys.exit(main())
