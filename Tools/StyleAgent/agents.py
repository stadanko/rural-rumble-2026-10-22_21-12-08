"""The three LLM roles. Each call is a fresh, tool-less `claude -p` session with its own
system prompt (prompts/*.md) and a JSON schema, so the Evaluator never sees the
Generator's or Refiner's reasoning, only the draft.
"""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROMPTS = HERE / "prompts"

HEX = {"type": "string", "pattern": "^#[0-9A-Fa-f]{6}$"}
DRAFT_SCHEMAS = {
    "art_brief": {
        "type": "object",
        "properties": {
            "subject": {"type": "string"},
            "prompt": {"type": "string"},
            "negative_prompt": {"type": "string"},
            "palette": {"type": "array", "items": HEX},
            "lighting": {"type": "string"},
            "details": {"type": "string"},
            "size_px": {"type": "object", "properties": {"width": {"type": "integer"}, "height": {"type": "integer"}},
                        "required": ["width", "height"], "additionalProperties": False},
            "scale_note": {"type": "string"},
            "layer": {"type": "string"},
        },
        "required": ["subject", "prompt", "negative_prompt", "palette", "lighting", "details", "size_px",
                     "scale_note", "layer"],
        "additionalProperties": False,
    },
    "trash_talk": {
        "type": "object",
        "properties": {
            "lines": {"type": "array", "items": {
                "type": "object",
                "properties": {"speaker": {"type": "string", "enum": ["barry", "peaux"]}, "text": {"type": "string"}},
                "required": ["speaker", "text"], "additionalProperties": False}},
        },
        "required": ["lines"],
        "additionalProperties": False,
    },
}

EVAL_SCHEMA = {
    "type": "object",
    "properties": {
        "scores": {"type": "array", "items": {
            "type": "object",
            "properties": {"rule_id": {"type": "string"}, "score": {"type": "integer", "minimum": 0, "maximum": 10},
                           "reason": {"type": "string"}, "evidence": {"type": "string"}, "fix": {"type": "string"}},
            "required": ["rule_id", "score", "reason", "evidence", "fix"], "additionalProperties": False}},
        "summary": {"type": "string"},
    },
    "required": ["scores", "summary"],
    "additionalProperties": False,
}


def refine_schema(kind):
    return {
        "type": "object",
        "properties": {
            "draft": DRAFT_SCHEMAS[kind],
            "changes": {"type": "array", "items": {
                "type": "object", "properties": {"rule_id": {"type": "string"}, "change": {"type": "string"}},
                "required": ["rule_id", "change"], "additionalProperties": False}},
        },
        "required": ["draft", "changes"],
        "additionalProperties": False,
    }


def _system(name):
    return (PROMPTS / f"{name}.md").read_text(encoding="utf-8")


def _context(task, docs, rulebook):
    return (f"<docs>\n{docs}\n</docs>\n\n<rules>\n{rulebook}\n</rules>\n\n"
            f'<brief kind="{task["kind"]}" id="{task["id"]}">\n{task["brief"]}\n</brief>')


def generate(llm, task, docs, rulebook):
    user = (_context(task, docs, rulebook) +
            f"\n\n<format>\n{json.dumps(DRAFT_SCHEMAS[task['kind']])}\n</format>\n\nWrite the draft.")
    return llm.complete_json("generator", _system("generator"), user, DRAFT_SCHEMAS[task["kind"]])


def evaluate(llm, task, docs, rulebook, draft):
    user = (_context(task, docs, rulebook) +
            f"\n\n<draft>\n{json.dumps(draft, indent=2, ensure_ascii=False)}\n</draft>\n\n"
            f"Score every rule: {', '.join(task['rules'])}.")
    return llm.complete_json("evaluator", _system("evaluator"), user, EVAL_SCHEMA)


def refine(llm, task, docs, rulebook, draft, scorecard):
    targets = [r for r in scorecard["rules"] if r["score"] < 10]
    lines = []
    for r in sorted(targets, key=lambda r: (r["score"], -r["weight"])):
        lines.append(f'- {r["rule_id"]} ({r["title"]}; weight {r["weight"]}{", HARD" if r["hard"] else ""}): '
                     f'{r["score"]}/10')
        if r.get("llm"):
            lines.append(f'  Evaluator: {r["llm"]["reason"]} | evidence: "{r["llm"]["evidence"]}" | fix: {r["llm"]["fix"]}')
        if r.get("check"):
            lines.append(f'  Automated check ({r["check"]["score"]}/10): {r["check"]["reason"]}')
    card = (f"Total {scorecard['total']}/100 (pass needs >= {scorecard['threshold']} and every hard rule >= "
            f"{scorecard['hard_floor']} and every other rule >= {scorecard.get('soft_floor')}).\n"
            f"Rules below 10, lowest first:\n" + "\n".join(lines))
    user = (_context(task, docs, rulebook) +
            f"\n\n<draft>\n{json.dumps(draft, indent=2, ensure_ascii=False)}\n</draft>\n\n"
            f"<scorecard>\n{card}\n</scorecard>\n\nFix the violations and return the revised draft.")
    return llm.complete_json("refiner", _system("refiner"), user, refine_schema(task["kind"]))
