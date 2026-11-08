"""Deterministic checks that run before the LLM critic: structure, UI length, and an
out-of-scope-mechanics guard. Every issue found here blocks approval, whatever the
LLM critic says, so the model can't wave through a submission or championship win.
"""
import re

from docs_loader import normalize_quotes
from specs import LIMITS

# Mechanics the MVP does not have. Each entry cites the doc that rules it out.
OUT_OF_SCOPE = [
    (r"\bsubmi(t|ts|tted|tting|ssion|ssions)\b|\btap(s|ped|ping)? out\b",
     "submission victory",
     'GDD "MVP Scope": Match Rules are "Health-based damage and knockout victory conditions." There are no submissions.'),
    (r"\bchampion(s|ship|ships)?\b|\btitle (belt|match|shot|defen[cs]e)\b|\b(world|heavyweight|tag) title\b",
     "championship / title",
     'GDD "MVP Scope" lists one match against the CPU ending in a knockout; the docs have no titles or championships.'),
    (r"\bpin(s|ned|ning|fall|falls)?\b|\b(three|3)[- ]count\b|\bcount of three\b",
     "pinfall",
     'GDD "MVP Scope": "Pinfall and three-count mechanics" are Stretch Goals, not MVP.'),
    (r"\btag[- ]team\b|\btagged (in|out)\b",
     "tag-team match",
     'GDD "MVP Scope": tag-team matches are Stretch Goals, not MVP.'),
    (r"\btournament\b|\bdisqualif\w*|\bDQ\b|\bcount[- ]?out\b|\b(cage|ladder|hardcore|title) match\b",
     "other match type",
     'GDD "Technical Constraints and Scope": alternative match types are deferred until the core gameplay is stable.'),
]


def _issue(category, location, explanation, recommendation, quote=""):
    return {"source": "validator", "category": category, "severity": "blocking",
            "location": location, "quote": quote,
            "explanation": explanation, "recommendation": recommendation}


def _text_ok(obj, key, location, issues):
    value = obj.get(key)
    if not isinstance(value, str) or not value.strip():
        issues.append(_issue("output_validity", location, f'"{key}" is missing, empty, or not a string.',
                             f'Provide a non-empty string for "{key}".'))
        return None
    return value.strip()


def _length(value, lo, hi, location, issues, what):
    n = len(value)
    if (lo is not None and n < lo) or n > hi:
        span = f"{lo}-{hi}" if lo is not None else f"at most {hi}"
        issues.append(_issue("ui_suitability", location, f"{what} is {n} characters; the UI limit is {span}.",
                             f"Rewrite {what} to {span} characters.", value))


def validate_structure(kind, obj, characters, character_id=None, peer=None):
    """Return blocking issues for malformed output. `characters` is {id: display name} from the GDD."""
    if not isinstance(obj, dict):
        return [_issue("output_validity", "$", "Output is not a JSON object.", "Return a JSON object.")]
    issues = []
    if kind == "character":
        expected = {"id", "display_name", "tagline", "description"}
        extra = set(obj) - expected
        if extra:
            issues.append(_issue("output_validity", "$", f"Unexpected fields: {sorted(extra)}.",
                                 "Return only id, display_name, tagline, description."))
        if obj.get("id") != character_id:
            issues.append(_issue("output_validity", "id", f'id is {obj.get("id")!r}; expected "{character_id}".',
                                 f'Set id to "{character_id}".'))
        name = _text_ok(obj, "display_name", "display_name", issues)
        if name and normalize_quotes(name) != characters[character_id]:
            issues.append(_issue("character_accuracy", "display_name",
                                 f'display_name must match the GDD name "{characters[character_id]}".',
                                 f'Use "{characters[character_id]}".', name))
        tagline = _text_ok(obj, "tagline", "tagline", issues)
        if tagline:
            _length(tagline, None, LIMITS["tagline_max"], "tagline", issues, "the tagline")
        desc = _text_ok(obj, "description", "description", issues)
        if desc:
            _length(desc, LIMITS["description_min"], LIMITS["description_max"], "description", issues,
                    "the description")
            if peer:
                ref = len(peer)
                tol = LIMITS["peer_length_tolerance"]
                if abs(len(desc) - ref) > ref * tol:
                    issues.append(_issue("ui_suitability", "description",
                                         f"Description is {len(desc)} characters; the style reference is {ref}. "
                                         f"They should be within {int(tol * 100)}% of each other.",
                                         f"Rewrite to roughly {ref} characters.", desc))
    elif kind == "victory":
        messages = obj.get("messages")
        if not isinstance(messages, list) or not messages:
            return [_issue("output_validity", "messages", '"messages" is missing or empty.',
                           'Return a non-empty "messages" list.')]
        counts = {cid: 0 for cid in characters}
        seen = set()
        for i, m in enumerate(messages):
            loc = f"messages[{i}]"
            if not isinstance(m, dict):
                issues.append(_issue("output_validity", loc, "Message is not an object.",
                                     'Use {"winner": ..., "text": ...}.'))
                continue
            if m.get("winner") not in characters:
                issues.append(_issue("output_validity", f"{loc}.winner",
                                     f"winner {m.get('winner')!r} is not a wrestler id ({sorted(characters)}).",
                                     f"Use one of {sorted(characters)}."))
            else:
                counts[m["winner"]] += 1
            text = _text_ok(m, "text", f"{loc}.text", issues)
            if not text:
                continue
            _length(text, None, LIMITS["victory_text_max"], f"{loc}.text", issues, "the message")
            if "{" in text or "}" in text:
                issues.append(_issue("output_validity", f"{loc}.text", "Message contains a placeholder or brace.",
                                     "Write the line out in full.", text))
            if text.lower() in seen:
                issues.append(_issue("output_validity", f"{loc}.text", "Duplicate message.",
                                     "Make every message unique.", text))
            seen.add(text.lower())
        lo, hi = LIMITS["victory_min_per_wrestler"], LIMITS["victory_max_per_wrestler"]
        for cid, n in counts.items():
            if not lo <= n <= hi:
                issues.append(_issue("output_validity", "messages",
                                     f'{n} message(s) for "{cid}"; need {lo}-{hi}.',
                                     f'Write {lo}-{hi} messages with winner "{cid}".'))
    return issues


def _strings(obj, path="$"):
    if isinstance(obj, str):
        yield path, obj
    elif isinstance(obj, dict):
        for k, v in obj.items():
            yield from _strings(v, k if path == "$" else f"{path}.{k}")
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            yield from _strings(v, f"{path}[{i}]" if path != "$" else f"[{i}]")


def mechanics_guard(obj):
    """Flag every out-of-scope match mechanic mentioned anywhere in the output."""
    issues = []
    for location, text in _strings(obj):
        for pattern, label, citation in OUT_OF_SCOPE:
            hit = re.search(pattern, text, re.IGNORECASE)
            if hit:
                issues.append({"source": "mechanics_guard", "category": "game_mechanics", "severity": "blocking",
                               "location": location, "quote": text,
                               "explanation": f'Mentions {label} ("{hit.group(0)}"). {citation}',
                               "recommendation": "Remove it. Matches in Rural Rumble end only by knockout."})
    return issues
