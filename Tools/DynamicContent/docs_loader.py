"""Loads the Rural Rumble design-doc sections that the generator and critic read.

Nothing about the characters or setting is hard-coded in this pipeline: every
lore fact comes from the docs listed in specs.DOC_SECTIONS. A missing doc or
heading stops the run instead of silently generating from nothing.
"""
import hashlib
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

_HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*$")
_IMAGE_ONLY_LINE = re.compile(r"^\s*(!\[[^\]]*\]\([^)]*\)\s*)+$")
_NAME_LINE = re.compile(r"^\*\*Name:\*\*\s*(.+?)\s*$", re.MULTILINE)


class DocError(RuntimeError):
    pass


def normalize_quotes(text):
    """Curly quotes -> straight quotes (pixel UI fonts rarely carry curly glyphs)."""
    return (text.replace("“", '"').replace("”", '"')
                .replace("‘", "'").replace("’", "'"))


def extract_section(text, heading):
    """Return the section under `heading` (exact text, or 'heading: ...'), minus image-only lines."""
    lines = text.splitlines()
    for i, line in enumerate(lines):
        m = _HEADING.match(line)
        if not m or not (m.group(2) == heading or m.group(2).startswith(heading + ":")):
            continue
        level = len(m.group(1))
        body = [line]
        for nxt in lines[i + 1:]:
            n = _HEADING.match(nxt)
            if n and len(n.group(1)) <= level:
                break
            body.append(nxt)
        return "\n".join(l for l in body if not _IMAGE_ONLY_LINE.match(l)).strip()
    raise DocError(f"Heading '{heading}' not found")


class DocPack:
    """The doc excerpts for one run, plus hashes so reports show exactly what was read."""

    def __init__(self, sections, root=REPO_ROOT):
        self.excerpts = []
        self.hashes = {}
        for rel, heading in sections:
            path = Path(root) / rel
            if not path.is_file():
                raise DocError(f"Required doc missing: {rel}")
            if rel not in self.hashes:
                self.hashes[rel] = hashlib.sha256(path.read_bytes()).hexdigest()
            try:
                body = extract_section(path.read_text(encoding="utf-8"), heading)
            except DocError as e:
                raise DocError(f"{rel}: {e}") from None
            self.excerpts.append((rel, heading, body))

    def as_prompt(self):
        return "\n\n".join(f'<doc source="{rel}" section="{heading}">\n{body}\n</doc>'
                           for rel, heading, body in self.excerpts)

    def manifest(self):
        return [{"path": rel, "sha256": sha,
                 "sections": [h for r, h, _ in self.excerpts if r == rel]}
                for rel, sha in self.hashes.items()]

    def characters(self):
        """{id: display name} from the GDD's '**Name:**' lines; id is the first name, lowercased."""
        names = []
        for rel, _, body in self.excerpts:
            if rel.endswith("GDD.md"):
                names += [normalize_quotes(n) for n in _NAME_LINE.findall(body)]
        if not names:
            raise DocError("No '**Name:**' lines found in the GDD Character Designs section")
        return {n.split()[0].lower(): n for n in names}
