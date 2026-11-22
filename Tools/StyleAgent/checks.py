"""Deterministic, graded checks. Each returns {"score": 0-10, "reason": str, "evidence": [..]}.

These never pass/fail on their own: they score a rule from 0 to 10 with a reason, and the
loop takes the lower of this score and the LLM Evaluator's score for the same rule. They
exist so hard, countable rules (palette hexes, banned scenery, KO-only wording, line length)
can't be waved through by a lenient model.
"""
import re

NEGATION = re.compile(r"\b(no|not|without|never|free of|avoid|lacking|zero|none)\b[\w\s,/-]{0,30}$", re.I)


def _result(score, reason, evidence=()):
    return {"score": max(0, min(10, round(score))), "reason": reason, "evidence": list(evidence)}


def _hits(text, patterns, negatable=True):
    """(label, matched text) per match. In art prompts a negated term ('no mountains') is skipped;
    in spoken lines it is not, since "Not before my piledriver" still names the move."""
    found = []
    for pat, label in patterns:
        for m in re.finditer(pat, text, re.I):
            if negatable and NEGATION.search(text[max(0, m.start() - 40):m.start()]):
                continue
            found.append((label, m.group(0)))
    return found


def art_text(draft):
    """What the image model will actually be told to draw (negative prompt excluded)."""
    return " ".join(str(draft.get(k, "")) for k in ("subject", "prompt", "lighting", "details"))


# ---------------------------------------------------------------- art brief checks

STYLE_GOOD = [r"\bpixel[- ]?art\b", r"\b(16-bit|16 bit|arcade|sprite)\b",
              r"\b(technos|capcom|snk|final fight|streets of rage|wrestlefest|fatal fury)\b"]
STYLE_BAD = [(r"\b3d\b|\bcgi\b|\bblender\b|\bunreal( engine)?\b|\boctane\b|\bray[- ]?trac\w*\b", "3D render"),
             (r"\bphoto(real|realistic|graph\w*)\b|\bhyper-?real\w*\b|\b(4k|8k|hdr)\b", "photorealism"),
             (r"\bvector\b|\bflat design\b", "vector art"),
             (r"\banime\b|\bmanga\b|\bcel[- ]shaded\b", "anime/cel-shaded"),
             (r"\bwatercolou?r\b|\boil paint\w*\b|\bpainterly\b|\bsmooth gradients?\b|\bsoft (focus|blur)\b", "painterly/smooth"),
             (r"\banti-?aliased\b", "anti-aliasing")]


def pixel_style(draft, task, ctx):
    text = art_text(draft)
    good = sum(1 for p in STYLE_GOOD if re.search(p, text, re.I))
    bad = _hits(text, STYLE_BAD)
    score = {0: 2, 1: 7}.get(good, 10) - 4 * len(bad)
    reason = f"{good}/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game)"
    if bad:
        reason += "; off-style terms: " + ", ".join(f'"{m}" ({lbl})' for lbl, m in bad)
    return _result(score, reason, [m for _, m in bad])


def palette(draft, task, ctx):
    pal = set(ctx["palette"])
    listed = [str(h).upper() for h in draft.get("palette", []) if isinstance(h, str)]
    in_prompt = [h.upper() for h in re.findall(r"#[0-9A-Fa-f]{6}\b", art_text(draft))]
    all_hex = list(dict.fromkeys(listed + in_prompt))
    if not all_hex:
        return _result(0, "No hex colours given; the brief must build its palette from the Style Guide's 14 colours.")
    off = [h for h in all_hex if h not in pal]
    on = [h for h in all_hex if h in pal]
    score = 10 * len(on) / len(all_hex)
    reason = f"{len(on)}/{len(all_hex)} hex colours are on the Style Guide palette"
    if off:
        reason += f"; off-palette: {', '.join(off)}"
    if len(on) < 4:
        score -= 3
        reason += f"; only {len(on)} palette colours used (need at least 4 for shading ramps)"
    if not in_prompt:
        score -= 2
        reason += "; the prompt text itself names no hex colours, so the image model never sees the palette"
    return _result(score, reason, off)


SCENERY = [(r"\bmountains?\b|\bmountainous\b|\bpeaks?\b|\bfoothills?\b", "mountains"),
           (r"\b(large|big|rolling|steep) hills?\b|\bhillside\b|\bcliffs?\b|\bcanyon\b|\bmesa\b", "large hills / cliffs"),
           (r"\bcity\b|\bcityscape\b|\bskyline\b|\bskyscrapers?\b|\bdowntown\b|\burban\b|\bhigh-?rise\b", "city"),
           (r"\bhighway\b|\binterstate\b|\boverpass\b|\bneon\b|\bbillboard\b|\bstreet ?lights?\b", "city fixtures"),
           (r"\bsnow(y|capped)?\b|\bdesert\b|\bpalm trees?\b|\bocean\b|\bbeach\b|\bcastle\b|\bcyberpunk\b", "wrong biome/genre")]


def banned_scenery(draft, task, ctx):
    hits = _hits(art_text(draft), SCENERY)
    if not hits:
        return _result(10, "No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt.")
    return _result(10 - 4 * len(hits), "Banned scenery in the prompt: " + ", ".join(f'"{m}" ({lbl})' for lbl, m in hits),
                   [m for _, m in hits])


GOLDEN = [r"golden[- ]hour", r"late[- ]afternoon", r"\b(warm )?orange (sky|light|glow)\b|\bwarm orange\b",
          r"long (cast )?shadows?"]
WRONG_LIGHT = [(r"\bmidday\b|\bnoon\b|\bnight(time)?\b|\bmoonlit\b|\bmidnight\b|\bovercast\b|\bdawn\b|\bsunrise\b|\bblue hour\b", "wrong time of day")]


def golden_hour(draft, task, ctx):
    text = art_text(draft)
    good = sum(1 for p in GOLDEN if re.search(p, text, re.I))
    bad = _hits(text, WRONG_LIGHT)
    score = [1, 5, 8, 10, 10][good] - 4 * len(bad)
    reason = f"{good}/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows)"
    if bad:
        reason += "; conflicting light: " + ", ".join(f'"{m}"' for _, m in bad)
    return _result(score, reason, [m for _, m in bad])


DECAY = r"\b(rust\w*|sag\w*|patch\w*|fad\w*|peel\w*|dent\w*|crack\w*|broken|missing|weed\w*|overgrown|stain\w*|mildew\w*|rott\w*|warp\w*|duct[- ]tape\w*|tarp\w*|lean\w*|corrod\w*|grime|grimy|weathered|busted|bent|chipped|moss\w*|junk)\b"
NEW = [(r"\bbrand[- ]new\b|\bpristine\b|\bspotless\b|\bfreshly painted\b|\bimmaculate\b|\bshiny new\b|\bwell[- ]kept\b|\bmanicured\b", "kept-up look")]


def disrepair(draft, task, ctx):
    text = art_text(draft)
    words = sorted({m.group(0).lower() for m in re.finditer(DECAY, text, re.I)})
    bad = _hits(text, NEW)
    score = min(10, 2 + 2 * len(words)) - 5 * len(bad)
    reason = f"{len(words)} distinct neglect details ({', '.join(words) or 'none'})"
    if bad:
        reason += "; contradicting kept-up wording: " + ", ".join(f'"{m}"' for _, m in bad)
    return _result(score, reason, [m for _, m in bad])


def roof_antenna(draft, task, ctx):
    text = art_text(draft)
    if re.search(r"\b(tv |television |rooftop |roof )?antenna\b|\b(satellite|directv|direct tv|dish) ?(dish)?\b", text, re.I) \
            and re.search(r"\broof\w*\b", text, re.I):
        return _result(10, "Roof carries a TV antenna or satellite dish.")
    if re.search(r"\bantenna\b|\bdish\b", text, re.I):
        return _result(6, "Antenna/dish mentioned but not placed on the roof.")
    return _result(0, "No old TV antenna or satellite dish on the trailer roof.")


def scale(draft, task, ctx):
    size = draft.get("size_px") or {}
    h = size.get("height")
    note = str(draft.get("scale_note", ""))
    spec = task.get("scale_px", {})
    lo, hi = spec.get("height_min"), spec.get("height_max")
    if not isinstance(h, (int, float)):
        return _result(0, "size_px.height missing; the brief must give the sprite's native-pixel height.")
    score, parts = 10, [f"height {h}px vs target {lo}-{hi}px ({spec.get('basis', '')})"]
    if lo is not None and h < lo:
        score -= min(8, 2 + (lo - h) / 40)
    elif hi is not None and h > hi:
        score -= min(8, 2 + (h - hi) / 40)
    if "240" not in note:
        score -= 3
        parts.append("scale_note does not tie the size to the 240-px wrestlers")
    return _result(score, "; ".join(parts))


# ---------------------------------------------------------------- trash talk checks

def talk_text(draft):
    return " ".join(str(l.get("text", "")) for l in draft.get("lines", []) if isinstance(l, dict))


OTHER_WRESTLERS = r"\b(hulk|hogan|jake roberts|duggan|bobby eaton|condry|darby|hardy|raven|kidman|stone ?cold|the rock|undertaker|macho man|andre)\b"


def names(draft, task, ctx):
    lines = [l for l in draft.get("lines", []) if isinstance(l, dict)]
    bad_speakers = [l.get("speaker") for l in lines if l.get("speaker") not in ctx["characters"]]
    text = talk_text(draft)
    others = sorted({m.group(0) for m in re.finditer(OTHER_WRESTLERS, text, re.I)})
    wrong_nick = re.findall(r'"([^"]{2,30})"', text)
    wrong_nick = [n for n in wrong_nick if n.lower() != "hot potato"]
    score = 10 - 5 * len(bad_speakers) - 5 * len(others) - 3 * len(wrong_nick)
    issues = []
    if bad_speakers:
        issues.append(f"unknown speakers {bad_speakers} (allowed: {sorted(ctx['characters'])})")
    if others:
        issues.append(f"other wrestlers named: {others}")
    if wrong_nick:
        issues.append(f"nicknames not in the GDD: {wrong_nick}")
    return _result(score, "; ".join(issues) or "Only Barry and Peaux speak; no other wrestlers or invented nicknames.",
                   [str(x) for x in bad_speakers + others + wrong_nick])


# Mirrors the Assignment #04 out-of-scope guard (Tools/DynamicContent/validate.py), with GDD citations there.
OUT_OF_SCOPE = [
    (r"\bsubmi(t|ts|tted|tting|ssion|ssions)\b|\btap(s|ped|ping)? out\b", "submission"),
    (r"\bchampion(s|ship|ships)?\b|\btitle\b|\bbelt\b", "title/belt"),
    (r"\bpin(s|ned|ning|fall|falls)?\b|\b(three|3)[- ]count\b|\bcount of three\b|\bone[- ]two[- ]three\b|\b1[, ]+2[, ]+3\b", "pinfall"),
    (r"\btag[- ]team\b|\btagged (in|out)\b", "tag team"),
    (r"\btournament\b|\bdisqualif\w*|\bDQ\b|\bcount[- ]?out\b|\b(cage|ladder|hardcore|title) match\b|\bring\b|\bturnbuckle\b|\bropes\b", "other match type / ring furniture"),
]


def mvp_mechanics(draft, task, ctx):
    hits = _hits(talk_text(draft), OUT_OF_SCOPE, negatable=False)
    if not hits:
        return _result(10, "No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes).")
    return _result(10 - 5 * len(hits), "Non-MVP mechanics mentioned: " + ", ".join(f'"{m}" ({lbl})' for lbl, m in hits),
                   [m for _, m in hits])


NON_MVP_MOVES = [(r"\bsuplex\w*\b|\bpiledriv\w*\b|\bDDT\b|\bpower ?bomb\w*\b|\bchoke ?slam\w*\b|\bclothesline\w*\b|"
                  r"\bleg ?drop\w*\b|\belbow ?drop\w*\b|\bmoonsault\w*\b|\bsplash\b|\bdropkick\w*\b|\bspear\w*\b|"
                  r"\bsleeper\b|\bheadlock\w*\b|\bfigure[- ]four\b|\bsharpshooter\b|\bstunner\b|\bfinisher\b|"
                  r"\bsignature move\b|\bfrog splash\b|\bneckbreaker\b|\bbackbreaker\b|\bpowerslam\b|\bfalling elbow\b|"
                  r"\bchair shot\b|\bsteel chair\b|\btable\b", "non-MVP move")]


def mvp_moves(draft, task, ctx):
    hits = _hits(talk_text(draft), NON_MVP_MOVES, negatable=False)
    if not hits:
        return _result(10, "Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none.")
    return _result(10 - 4 * len(hits), "Moves the game doesn't have: " + ", ".join(f'"{m}"' for _, m in hits),
                   [m for _, m in hits])


NUMBER_WORDS = ("one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen "
                "seventeen eighteen nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred thousand").split()
_NUMBER = re.compile(r"\b(\d+|" + "|".join(NUMBER_WORDS) + r")\b", re.I)


def doc_numbers(doc_text):
    """Every number (digits or words) the docs themselves state, lowercased."""
    return {m.group(0).lower() for m in _NUMBER.finditer(doc_text)}


def _invented_numbers(speaker):
    """Numbers a wrestler states that the docs never state ("Thirty years hauling freight" for an
    early-thirties Barry; "four brothers" when the GDD gives no count). 'One' is allowed: it is
    usually 'One Irish Whip', not a fact."""
    def check(draft, task, ctx):
        text = " ".join(str(l.get("text", "")) for l in draft.get("lines", [])
                        if isinstance(l, dict) and l.get("speaker") == speaker)
        bad = sorted({m.group(0) for m in _NUMBER.finditer(text)
                      if m.group(0).lower() not in ctx.get("doc_numbers", set()) | {"one"}})
        if not bad:
            return _result(10, f"{speaker.title()} states no numbers the docs don't (ages, years, counts).")
        return _result(10 - 5 * len(bad), f"{speaker.title()} states numbers the docs never give: {bad}. "
                       "Invented ages, years or counts can contradict the GDD/Style Guide; cut the number.", bad)
    check.__name__ = f"doc_facts_{speaker}"
    return check


SETTING_PLACES =r"\b(duckville|west mcrae|mcrae|hoon|rayboro|moldon|sticksburg)\b"
SETTING_WORDS = r"\b(trailers?|gravel|driveways?|dirt( lot| track)?|lot rent|carport|porch|septic|swamp|pines?|cigarette butts?|trailer park|single-wide|double-wide)\b"


def setting_anchor(draft, task, ctx):
    text = talk_text(draft)
    places = sorted({m.group(0).lower() for m in re.finditer(SETTING_PLACES, text, re.I)})
    words = sorted({m.group(0).lower() for m in re.finditer(SETTING_WORDS, text, re.I)})
    score = (5 if places else 0) + min(5, 2 * len(words) + (1 if words else 0))
    return _result(score, f"place names: {places or 'none'}; trailer-park details: {words or 'none'}")


def line_format(draft, task, ctx):
    lines = draft.get("lines")
    if not isinstance(lines, list) or not lines:
        return _result(0, "No lines.")
    issues, score = [], 10
    if len(lines) != 4:
        score -= 3
        issues.append(f"{len(lines)} lines (need exactly 4)")
    speakers = [l.get("speaker") for l in lines if isinstance(l, dict)]
    if any(a == b for a, b in zip(speakers, speakers[1:])):
        score -= 3
        issues.append("speakers don't alternate")
    long_ = [len(str(l.get("text", ""))) for l in lines if isinstance(l, dict) and len(str(l.get("text", ""))) > 90]
    if long_:
        score -= 2 * len(long_)
        issues.append(f"{len(long_)} line(s) over 90 characters ({long_})")
    return _result(score, "; ".join(issues) or "4 alternating lines, all within 90 characters.")


CHECKERS = {f.__name__: f for f in (pixel_style, palette, banned_scenery, golden_hour, disrepair, roof_antenna, scale,
                                     names, mvp_mechanics, mvp_moves, setting_anchor, line_format,
                                     _invented_numbers("barry"), _invented_numbers("peaux"))}


def run_checks(draft, task, rules, ctx):
    """{rule_id: result} for every task rule that has a deterministic checker."""
    out = {}
    for rid in task["rules"]:
        name = rules[rid].get("checker")
        if name:
            out[rid] = CHECKERS[name](draft, task, ctx)
    return out
