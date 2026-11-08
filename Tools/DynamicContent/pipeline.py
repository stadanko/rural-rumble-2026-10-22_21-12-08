"""Rural Rumble Dynamic Content Pipeline (Assignment #04).

    python Tools/DynamicContent/pipeline.py              # all three outputs
    python Tools/DynamicContent/pipeline.py --only victory_messages

Docs -> Generator -> structural validation + Critic -> approved? save : revise (max 2) -> reject.
Approved files go to outputs/; rejected ones go to outputs/rejected/ and are never
written to outputs/. Every attempt is recorded in reports/critic_results.json.
"""
import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from critic import review
from docs_loader import DocPack
from generate import generate
from llm import DEFAULT_MODEL, ClaudeCliBackend, LLMError
from specs import DOC_SECTIONS, MAX_REVISIONS, SPECS

HERE = Path(__file__).resolve().parent
OUTPUTS = HERE / "outputs"
REJECTED = OUTPUTS / "rejected"
REPORTS = HERE / "reports"


def now():
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def run_output(backend, key, docs, characters, approved=None, max_revisions=MAX_REVISIONS,
               initial_draft=None, log=print):
    """Generate -> critique -> revise one output. Returns a record of every attempt.

    `approved` holds content already approved this run (used for Peaux's style reference).
    `initial_draft` skips the first generation and critiques the given draft instead
    (used only by the consistency check's deliberately inconsistent fixture).
    """
    spec = SPECS[key]
    ref = (approved or {}).get(spec.get("style_reference"))
    peer = ref.get("description") if ref else None
    record = {"output": key, "file": spec["filename"], "attempts": [], "status": None}
    candidate, issues = None, None
    for revision in range(max_revisions + 1):
        attempt = {"revision_attempt": revision}
        if revision == 0 and initial_draft is not None:
            candidate, attempt["generation"] = initial_draft, {"source": "fixture (not model-generated)"}
        else:
            log(f"  [{key}] {'generating' if revision == 0 else f'revision {revision}'}...")
            candidate, attempt["generation"] = generate(backend, spec, docs, characters,
                                                        previous=candidate, issues=issues,
                                                        style_reference=ref)
        attempt["candidate"] = candidate
        log(f"  [{key}] critic reviewing (revision attempt {revision})...")
        report = review(backend, spec, candidate, docs, characters, revision, peer)
        attempt["critic"] = report
        record["attempts"].append(attempt)
        log(f"  [{key}] critic: {'APPROVED' if report['approved'] else 'REJECTED'} "
            f"({report['blocking_count']} blocking, {len(report['issues'])} total issues)")
        if report["approved"]:
            record["status"] = "approved"
            record["final"] = candidate
            return record
        issues = report["issues"]
    record["status"] = "rejected"
    record["final"] = candidate
    record["unresolved_issues"] = [i for i in issues if i.get("severity") == "blocking"]
    return record


def total_cost(records):
    """Sum of reported model cost (USD) across every generator and critic call."""
    calls = [c for r in records for a in r.get("attempts", [])
             for c in (a["generation"], a["critic"]["call"])]
    return round(sum(c.get("cost_usd") or 0 for c in calls), 4)


def export(record, docs, backend, out_dir=OUTPUTS, rejected_dir=REJECTED):
    """Write approved content to outputs/, rejected content only to outputs/rejected/."""
    last = record["attempts"][-1]
    provenance = {
        "origin": "AI-generated",
        "review_status": "critic-approved; pending designer review" if record["status"] == "approved"
                         else "REJECTED by critic; do not use",
        "pipeline": "Tools/DynamicContent/pipeline.py",
        "model": getattr(backend, "model", None),
        "backend": getattr(backend, "name", None),
        "generated_at": now(),
        "revision_attempts": last["revision_attempt"],
        "source_docs": docs.manifest(),
    }
    body = dict(record["final"]) if isinstance(record["final"], dict) else {"raw": record["final"]}
    if record["status"] == "approved":
        path = Path(out_dir) / record["file"]
    else:
        path = Path(rejected_dir) / record["file"]
        body["unresolved_issues"] = record["unresolved_issues"]
    body["provenance"] = provenance
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(body, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return path


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--only", action="append", choices=list(SPECS), help="generate just this output (repeatable)")
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--max-revisions", type=int, default=MAX_REVISIONS, choices=range(MAX_REVISIONS + 1))
    args = ap.parse_args(argv)

    docs = DocPack(DOC_SECTIONS)
    characters = docs.characters()
    backend = ClaudeCliBackend(args.model)
    keys = args.only or list(SPECS)
    print(f"Read {len(docs.excerpts)} sections from {', '.join(docs.hashes)}; characters: {characters}")

    run = {"run_started": now(), "backend": backend.name, "model": backend.model,
           "max_revisions": args.max_revisions, "docs": docs.manifest(), "outputs": {}}
    approved = {}
    if "peaux_character" in keys and "barry_character" not in keys:
        barry = OUTPUTS / SPECS["barry_character"]["filename"]
        if barry.exists():  # reuse the last approved Barry text as Peaux's style reference
            approved["barry_character"] = json.loads(barry.read_text(encoding="utf-8"))
    failed = False
    for key in keys:
        print(f"== {key}")
        try:
            record = run_output(backend, key, docs, characters, approved, args.max_revisions)
        except LLMError as e:
            print(f"  ERROR: {e}")
            run["outputs"][key] = {"output": key, "status": "error", "error": str(e)}
            failed = True
            continue
        path = export(record, docs, backend)
        record["written_to"] = str(path.relative_to(HERE))
        run["outputs"][key] = record
        if record["status"] == "approved":
            approved[key] = record["final"]
        else:
            failed = True
        print(f"  -> {record['status'].upper()}: {record['written_to']}")
    run["run_finished"] = now()
    run["cost_usd"] = total_cost(run["outputs"].values())

    REPORTS.mkdir(exist_ok=True)
    text = json.dumps(run, indent=2, ensure_ascii=False) + "\n"
    (REPORTS / "critic_results.json").write_text(text, encoding="utf-8")
    stamp = run["run_started"].replace(":", "").replace("+0000", "Z")
    (REPORTS / "runs").mkdir(exist_ok=True)
    (REPORTS / "runs" / f"run_{stamp}.json").write_text(text, encoding="utf-8")
    print(f"Report: reports/critic_results.json (archived as reports/runs/run_{stamp}.json); "
          f"cost ${run['cost_usd']}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
