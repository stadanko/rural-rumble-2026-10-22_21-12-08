"""Content Critic stage: deterministic checks plus an independent LLM review.

The LLM critic is not shown the deterministic findings, so each layer is a
separate check; their issues are merged and any blocking issue rejects.

The critic's instructions are the body of .claude/agents/content-critic.md, so the
pipeline stage and the interactive Claude Code agent share one definition.
"""
import json
from pathlib import Path

from docs_loader import REPO_ROOT
from generate import task_brief
from validate import mechanics_guard, validate_structure

CRITIC_AGENT = REPO_ROOT / ".claude" / "agents" / "content-critic.md"
CATEGORIES = ["character_accuracy", "game_mechanics", "tone_style", "ui_suitability", "output_validity"]

CRITIC_SCHEMA = {
    "type": "object",
    "properties": {
        "approved": {"type": "boolean"},
        "summary": {"type": "string"},
        "issues": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "category": {"type": "string", "enum": CATEGORIES},
                    "severity": {"type": "string", "enum": ["blocking", "minor"]},
                    "location": {"type": "string"},
                    "quote": {"type": "string"},
                    "explanation": {"type": "string"},
                    "recommendation": {"type": "string"},
                },
                "required": ["category", "severity", "location", "quote", "explanation", "recommendation"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["approved", "summary", "issues"],
    "additionalProperties": False,
}


def critic_instructions(path=CRITIC_AGENT):
    text = Path(path).read_text(encoding="utf-8")
    if text.startswith("---"):
        text = text.split("---", 2)[2]
    return text.strip()


def review(backend, spec, candidate, docs, characters, revision_attempt, peer_description=None):
    """Return the critic report for one candidate. Approved only if nothing blocks."""
    deterministic = (validate_structure(spec["kind"], candidate, characters, spec.get("character"),
                                        peer_description)
                     + mechanics_guard(candidate))
    user = "\n\n".join([
        docs.as_prompt(),
        "<task_given_to_generator>\n" + task_brief(spec, characters) + "\n</task_given_to_generator>",
        "<candidate>\n" + json.dumps(candidate, indent=2, ensure_ascii=False) + "\n</candidate>",
        f"This is revision attempt {revision_attempt} (0 = first draft). Review the candidate.",
    ])
    verdict, meta = backend.complete_json("critic", critic_instructions(), user, CRITIC_SCHEMA)
    llm_issues = [dict(i, source="llm_critic") for i in verdict.get("issues", [])]
    issues = deterministic + llm_issues
    blocking = [i for i in issues if i.get("severity") == "blocking"]
    approved = bool(verdict.get("approved")) and not blocking
    return {
        "revision_attempt": revision_attempt,
        "approved": approved,
        "llm_verdict": {"approved": bool(verdict.get("approved")), "summary": verdict.get("summary", "")},
        "blocking_count": len(blocking),
        "issues": issues,
        "call": meta,
    }
