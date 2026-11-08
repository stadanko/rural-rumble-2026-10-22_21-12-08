"""Consistency-check demonstration (Assignment #04, section 6).

    python Tools/DynamicContent/consistency_check.py

Feeds a deliberately inconsistent, hand-written victory-message draft
(fixtures/inconsistent_victory_messages.json: "Barry defeated Peaux by submission
to win the world championship.") into the real critic -> revise -> re-critic loop.
Passes only if the first review rejects the draft for BOTH the submission and the
championship, and the loop ends with an approved revision free of both.

Evidence goes to reports/consistency_check.json. The revised text is NOT written to
outputs/: this is a controlled test, separate from the three required outputs.
"""
import argparse
import json
import sys
from pathlib import Path

from docs_loader import DocPack
from llm import DEFAULT_MODEL, ClaudeCliBackend
from pipeline import REPORTS, now, run_output, total_cost
from specs import DOC_SECTIONS
from validate import mechanics_guard

HERE = Path(__file__).resolve().parent
FIXTURE = HERE / "fixtures" / "inconsistent_victory_messages.json"
PROBLEMS = {"submission": ("submission", "submit", "tap out"), "championship": ("champion", "title")}


def caught(issues, words, source=None):
    """True if a blocking issue (optionally from one source) explains the problem named by `words`."""
    return any(i.get("severity") == "blocking" and (source is None or i.get("source") == source)
               and any(w in i.get("explanation", "").lower() for w in words)
               for i in issues)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Run the deliberate-inconsistency test.")
    ap.add_argument("--model", default=DEFAULT_MODEL)
    args = ap.parse_args(argv)

    fixture = json.loads(FIXTURE.read_text(encoding="utf-8"))
    draft = {k: v for k, v in fixture.items() if not k.startswith("_")}
    docs = DocPack(DOC_SECTIONS)
    backend = ClaudeCliBackend(args.model)
    print(f"Injected (deliberate) line: {draft['messages'][0]['text']!r}")

    record = run_output(backend, "victory_messages", docs, docs.characters(), initial_draft=draft)
    first = record["attempts"][0]["critic"]
    final = record["final"]
    checks = {
        "first_review_rejected": not first["approved"],
        "submission_flagged": caught(first["issues"], PROBLEMS["submission"]),
        "championship_flagged": caught(first["issues"], PROBLEMS["championship"]),
        "submission_flagged_by_llm_critic": caught(first["issues"], PROBLEMS["submission"], "llm_critic"),
        "championship_flagged_by_llm_critic": caught(first["issues"], PROBLEMS["championship"], "llm_critic"),
        "revision_attempted": len(record["attempts"]) > 1,
        "final_status_approved": record["status"] == "approved",
        "final_free_of_out_of_scope_mechanics": not mechanics_guard(final),
    }
    required = ["first_review_rejected", "submission_flagged", "championship_flagged",
                "revision_attempted", "final_status_approved", "final_free_of_out_of_scope_mechanics"]
    passed = all(checks[k] for k in required)

    evidence = {
        "note": "Controlled test. The inconsistency was deliberately injected from a hand-written fixture; "
                "the critic did not discover it spontaneously. Revisions and reviews are live model calls.",
        "run_at": now(), "model": backend.model, "backend": backend.name, "fixture": str(FIXTURE.relative_to(HERE)),
        "original_content": draft,
        "critic_findings_on_original": first,
        "revisions": [{"revision_attempt": a["revision_attempt"], "content": a["candidate"],
                       "critic": a["critic"]} for a in record["attempts"][1:]],
        "final_status": record["status"],
        "final_content": final,
        "checks": checks,
        "result": "PASS" if passed else "FAIL",
        "cost_usd": total_cost([record]),
    }
    REPORTS.mkdir(exist_ok=True)
    (REPORTS / "consistency_check.json").write_text(json.dumps(evidence, indent=2, ensure_ascii=False) + "\n",
                                                    encoding="utf-8")
    for k, v in checks.items():
        print(f"  {'ok ' if v else 'NO '} {k}{'' if k in required else ' (informational)'}")
    print(f"Consistency check: {evidence['result']} -> reports/consistency_check.json")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
