"""What the pipeline generates: doc sources, output specs, UI limits, and JSON schemas.

UI length limits are pipeline assumptions (the docs give no character counts);
they are sized for the 960x540 native resolution in STYLE_GUIDE "Sprite Scale"
and are flagged for designer review in the README.
"""

# Docs every generator and critic call reads (path relative to repo root, heading).
DOC_SECTIONS = [
    ("docs/GDD.md", "Executive Summary"),
    ("docs/GDD.md", "Design Pillars: What is Central to the Experience?"),
    ("docs/GDD.md", "MVP Scope"),
    ("docs/GDD.md", "Core Gameplay"),
    ("docs/GDD.md", "Character Designs"),
    ("docs/GDD.md", "Setting & World"),
    ("docs/GDD.md", "Tone & Aesthetics"),
    ("docs/GDD.md", "Narrative"),
    ("docs/STYLE_GUIDE.md", "Overall Game Style"),
    ("docs/STYLE_GUIDE.md", "Characters"),
    ("docs/STYLE_GUIDE.md", "UI"),
    ("docs/DECISION_LOG.md", "D-003"),
    ("docs/DECISION_LOG.md", "D-026"),
]

LIMITS = {
    "tagline_max": 40,
    "description_min": 80,
    "description_max": 220,
    "victory_text_max": 90,
    "victory_min_per_wrestler": 2,
    "victory_max_per_wrestler": 4,
    # Peaux's description must stay close to Barry's length (assignment: "same general length").
    "peer_length_tolerance": 0.35,
}

MAX_REVISIONS = 2

CHARACTER_SCHEMA = {
    "type": "object",
    "properties": {
        "id": {"type": "string"},
        "display_name": {"type": "string"},
        "tagline": {"type": "string"},
        "description": {"type": "string"},
    },
    "required": ["id", "display_name", "tagline", "description"],
    "additionalProperties": False,
}

VICTORY_SCHEMA = {
    "type": "object",
    "properties": {
        "messages": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"winner": {"type": "string"}, "text": {"type": "string"}},
                "required": ["winner", "text"],
                "additionalProperties": False,
            },
        },
    },
    "required": ["messages"],
    "additionalProperties": False,
}

_CHARACTER_BRIEF = """Write the Character Select screen text for {name} (id "{id}").
STYLE_GUIDE "UI" says the Character Select screen shows each wrestler's profile image with the name underneath; this text sits beside that portrait.
Fields:
- id: "{id}"
- display_name: exactly "{name}"
- tagline: one punchy line, at most {tagline_max} characters.
- description: {description_min}-{description_max} characters, two or three short sentences.
Use only the background, personality, and look the docs give {first}. Do not invent major biography, titles, records, or special abilities. {extra}"""

SPECS = {
    "barry_character": {
        "kind": "character",
        "character": "barry",
        "filename": "barry_character.json",
        "brief": _CHARACTER_BRIEF,
        "extra": "Reflect his long-haul trucking past and his dirt racing track.",
    },
    "peaux_character": {
        "kind": "character",
        "character": "peaux",
        "filename": "peaux_character.json",
        "brief": _CHARACTER_BRIEF,
        "extra": "Reflect his trailer-park goth identity and his backyard wrestling. "
                 "Match the writing style and length of the style reference if one is given.",
        "style_reference": "barry_character",
    },
    "victory_messages": {
        "kind": "victory",
        "filename": "victory_messages.json",
        "brief": """Write short, character-specific flavor lines for the Victory screen.
STYLE_GUIDE "UI" fixes the screen's main line as "Victory! {{Winner}} knocked out {{Loser}} in {{timer}}"; each flavor line shows under it, in the winner's voice or about the winner.
Output "messages": a list of objects with "winner" (a wrestler id: {ids}) and "text".
- {victory_min_per_wrestler}-{victory_max_per_wrestler} messages per wrestler.
- Each text at most {victory_text_max} characters, no placeholders or braces.
- The only way a match ends is a knockout. Lines must fit a knockout win, and may mention the opponent by first name.
- Each line must sound like that wrestler's personality from the docs.""",
    },
}
