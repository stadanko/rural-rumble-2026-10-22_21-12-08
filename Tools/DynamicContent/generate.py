"""Content Generator stage: builds the prompt from the docs and asks the model for JSON."""
import json
from pathlib import Path

from specs import CHARACTER_SCHEMA, LIMITS, VICTORY_SCHEMA

GENERATOR_PROMPT = Path(__file__).resolve().parent / "prompts" / "generator.md"


def schema_for(spec):
    return CHARACTER_SCHEMA if spec["kind"] == "character" else VICTORY_SCHEMA


def task_brief(spec, characters):
    """The spec's brief with names and limits filled in from the docs and specs.LIMITS."""
    fields = dict(LIMITS, ids=", ".join(f'"{c}"' for c in characters), extra=spec.get("extra", ""))
    if spec["kind"] == "character":
        name = characters[spec["character"]]
        fields.update(id=spec["character"], name=name, first=name.split()[0])
    return spec["brief"].format(**fields)


def build_prompt(spec, docs, characters, previous=None, issues=None, style_reference=None):
    parts = [docs.as_prompt(), "<task>\n" + task_brief(spec, characters) + "\n</task>"]
    if style_reference:
        parts.append("<style_reference note=\"Approved text for the other wrestler. Match its style and length; "
                     "do not copy its jokes.\">\n" + json.dumps(style_reference, indent=2, ensure_ascii=False)
                     + "\n</style_reference>")
    if previous is not None:
        parts.append("<previous_draft>\n" + json.dumps(previous, indent=2, ensure_ascii=False) + "\n</previous_draft>")
        findings = [{k: i.get(k) for k in ("severity", "category", "location", "quote", "explanation",
                                            "recommendation")} for i in issues or []]
        parts.append("<critic_findings>\n" + json.dumps(findings, indent=2, ensure_ascii=False)
                     + "\n</critic_findings>\nRevise the previous draft to resolve every finding.")
    return "\n\n".join(parts)


def generate(backend, spec, docs, characters, previous=None, issues=None, style_reference=None):
    """Return (candidate JSON, call metadata)."""
    user = build_prompt(spec, docs, characters, previous, issues, style_reference)
    role = "generator" if previous is None else "generator-revision"
    return backend.complete_json(role, GENERATOR_PROMPT.read_text(encoding="utf-8"), user, schema_for(spec))
