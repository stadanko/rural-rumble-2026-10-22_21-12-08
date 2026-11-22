"""Loads the style rulebook and the task list, and proves every rule comes from the docs.

A rule is only usable if each of its `source.quotes` appears verbatim (after quote/markdown
normalisation) in the doc section it cites. If someone edits the GDD or Style Guide so a
quote disappears, the agent stops instead of enforcing a rule the docs no longer contain.
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
sys.path.insert(0, str(REPO_ROOT / "Tools" / "DynamicContent"))

from docs_loader import DocError, DocPack, extract_section  # noqa: E402  (shared with Assignment #04)

RULES_PATH = HERE / "style_rules.json"
TASKS_PATH = HERE / "tasks.json"
_HEX = re.compile(r"#[0-9A-Fa-f]{6}\b")


class RuleError(RuntimeError):
    pass


def norm(text):
    """Fold curly quotes, markdown emphasis/code marks and whitespace so quotes compare as plain text."""
    text = (text.replace("“", '"').replace("”", '"')
                .replace("‘", "'").replace("’", "'"))
    text = re.sub(r"[*_`]", "", text)
    return re.sub(r"\s+", " ", text).strip().lower()


def _doc_text(rel, root):
    path = Path(root) / rel
    if not path.is_file():
        raise RuleError(f"Doc missing: {rel}")
    return path.read_text(encoding="utf-8")


def verify_rule_sources(rules, root=REPO_ROOT):
    """Return a list of {rule_id, path, section, quote, found} for every cited quote."""
    results, cache = [], {}
    for rule in rules:
        for src in (rule.get("source"), rule.get("also_source")):
            if not src:
                continue
            key = (src["path"], src["section"])
            if key not in cache:
                try:
                    cache[key] = norm(extract_section(_doc_text(src["path"], root), src["section"]))
                except DocError as e:
                    raise RuleError(f"{rule['id']}: {src['path']}: {e}") from None
            for q in src["quotes"]:
                results.append({"rule_id": rule["id"], "path": src["path"], "section": src["section"],
                                "quote": q, "found": norm(q) in cache[key]})
    return results


def load_rules(root=REPO_ROOT, path=RULES_PATH):
    rules = json.loads(Path(path).read_text(encoding="utf-8"))["rules"]
    ids = [r["id"] for r in rules]
    if len(ids) != len(set(ids)):
        raise RuleError("Duplicate rule ids in style_rules.json")
    missing = [v for v in verify_rule_sources(rules, root) if not v["found"]]
    if missing:
        lines = "\n".join(f"  {m['rule_id']}: \"{m['quote']}\" not in {m['path']} > {m['section']}" for m in missing)
        raise RuleError("Rule quotes not found in the docs, refusing to run:\n" + lines)
    return {r["id"]: r for r in rules}


def load_tasks(rules, path=TASKS_PATH):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    for t in data["tasks"]:
        unknown = [rid for rid in t["rules"] if rid not in rules]
        if unknown:
            raise RuleError(f"Task {t['id']} names unknown rules {unknown}")
        if t["docs"] not in data["doc_sections"]:
            raise RuleError(f"Task {t['id']} names unknown doc set {t['docs']!r}")
    return data["tasks"], data["doc_sections"]


def palette_from_docs(root=REPO_ROOT):
    """The 14 palette hexes, read from STYLE_GUIDE 'Color Palette' (never hard-coded)."""
    section = extract_section(_doc_text("docs/STYLE_GUIDE.md", root), "Color Palette")
    hexes = [h.upper() for h in _HEX.findall(section)]
    if len(hexes) < 4:
        raise RuleError("STYLE_GUIDE 'Color Palette' has fewer than 4 hex colours")
    return list(dict.fromkeys(hexes))


def rulebook_prompt(rules, rule_ids):
    """The rules for one task, as the Generator, Evaluator and Refiner all see them."""
    out = []
    for rid in rule_ids:
        r = rules[rid]
        quotes = list(r["source"]["quotes"]) + list((r.get("also_source") or {}).get("quotes", []))
        cite = f'{r["source"]["path"]} > "{r["source"]["section"]}"'
        out.append(f'<rule id="{rid}" weight="{r["weight"]}" hard="{str(r["hard"]).lower()}">\n'
                   f'{r["title"]}: {r["rule"]}\n'
                   f'Source: {cite}. Doc text: ' + " / ".join(f'"{q}"' for q in quotes) + "\n</rule>")
    return "\n".join(out)


def doc_pack(doc_sections, which, root=REPO_ROOT):
    return DocPack([tuple(s) for s in doc_sections[which]], root=root)
