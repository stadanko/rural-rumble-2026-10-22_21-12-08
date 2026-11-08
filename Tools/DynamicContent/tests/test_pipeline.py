"""Offline tests for the Dynamic Content Pipeline. They use a scripted MockBackend
instead of Claude, so they test the loop, validator, and exporter; they say nothing
about content quality.

    python -m unittest discover -s Tools/DynamicContent/tests -v
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from docs_loader import DocError, DocPack, extract_section  # noqa: E402
from generate import build_prompt  # noqa: E402
from pipeline import export, run_output  # noqa: E402
from specs import DOC_SECTIONS, MAX_REVISIONS, SPECS  # noqa: E402
from validate import mechanics_guard, validate_structure  # noqa: E402

DOCS = DocPack(DOC_SECTIONS)
CHARS = DOCS.characters()

GOOD_VICTORY = {"messages": [
    {"winner": "barry", "text": "Barry flattened Peaux like a beer can on a tailgate."},
    {"winner": "barry", "text": "Pick up your butts, boy. Cigarette and otherwise."},
    {"winner": "peaux", "text": "Peaux left the redneck snoring in the gravel."},
    {"winner": "peaux", "text": "Eyeliner intact. Barry's dignity, not so much."},
]}
BAD_VICTORY = {"messages": [dict(m) for m in GOOD_VICTORY["messages"]]}
BAD_VICTORY["messages"][0]["text"] = "Barry defeated Peaux by submission to win the world championship."


class MockBackend:
    """Returns scripted generator outputs in order; the critic always says `critic_approves`."""
    name, model = "mock", "mock"

    def __init__(self, drafts, critic_approves=True):
        self.drafts = list(drafts)
        self.critic_approves = critic_approves
        self.calls = []

    def complete_json(self, role, system, user, schema):
        self.calls.append(role)
        if role == "critic":
            issues = [] if self.critic_approves else [{
                "category": "tone_style", "severity": "blocking", "location": "$", "quote": "",
                "explanation": "Mock rejection.", "recommendation": "Mock fix."}]
            return {"approved": self.critic_approves, "summary": "mock", "issues": issues}, {"cost_usd": 0}
        return self.drafts.pop(0), {"cost_usd": 0}


def quiet(*_):
    pass


class DocsTests(unittest.TestCase):
    def test_reads_real_project_docs(self):
        self.assertIn("docs/GDD.md", DOCS.hashes)
        self.assertIn("docs/STYLE_GUIDE.md", DOCS.hashes)
        self.assertIn("Hoon, Louisiana", DOCS.as_prompt())

    def test_character_names_come_from_gdd(self):
        self.assertEqual(CHARS, {"barry": 'Barry "Hot Potato" Lankford', "peaux": "Peaux Strange"})

    def test_missing_heading_fails_loudly(self):
        with self.assertRaises(DocError):
            extract_section("# A\ntext", "Nope")


class ValidatorTests(unittest.TestCase):
    def test_flags_submission_and_championship(self):
        labels = " ".join(i["explanation"] for i in mechanics_guard(BAD_VICTORY))
        self.assertIn("submission", labels)
        self.assertIn("championship", labels)

    def test_good_victory_passes(self):
        self.assertEqual(validate_structure("victory", GOOD_VICTORY, CHARS) + mechanics_guard(GOOD_VICTORY), [])

    def test_victory_needs_two_per_wrestler_and_valid_ids(self):
        bad = {"messages": [{"winner": "hulk", "text": "x"}, {"winner": "barry", "text": "y"}]}
        text = json.dumps(validate_structure("victory", bad, CHARS))
        self.assertIn("not a wrestler id", text)
        self.assertIn('for \\"peaux\\"', text)

    def test_character_checks(self):
        cand = {"id": "po", "display_name": "Po Strange", "tagline": "x" * 60, "description": "short"}
        cats = {i["location"] for i in validate_structure("character", cand, CHARS, "peaux")}
        self.assertEqual(cats, {"id", "display_name", "tagline", "description"})

    def test_peer_length_parity(self):
        cand = {"id": "peaux", "display_name": "Peaux Strange", "tagline": "Goth.", "description": "y" * 210}
        issues = validate_structure("character", cand, CHARS, "peaux", peer="x" * 100)
        self.assertTrue(any("style reference" in i["explanation"] for i in issues))


class LoopTests(unittest.TestCase):
    def test_approves_first_good_draft(self):
        backend = MockBackend([GOOD_VICTORY])
        rec = run_output(backend, "victory_messages", DOCS, CHARS, log=quiet)
        self.assertEqual(rec["status"], "approved")
        self.assertEqual(backend.calls, ["generator", "critic"])

    def test_revises_after_rejection(self):
        backend = MockBackend([BAD_VICTORY, GOOD_VICTORY])
        rec = run_output(backend, "victory_messages", DOCS, CHARS, log=quiet)
        self.assertEqual(rec["status"], "approved")
        self.assertEqual([a["revision_attempt"] for a in rec["attempts"]], [0, 1])
        self.assertFalse(rec["attempts"][0]["critic"]["approved"])

    def test_guard_overrides_an_approving_llm(self):
        # The mock LLM critic approves everything; the deterministic guard must still reject.
        backend = MockBackend([BAD_VICTORY] * 3, critic_approves=True)
        rec = run_output(backend, "victory_messages", DOCS, CHARS, log=quiet)
        self.assertEqual(rec["status"], "rejected")

    def test_exhausts_after_two_revisions_and_rejects(self):
        backend = MockBackend([GOOD_VICTORY] * 5, critic_approves=False)
        rec = run_output(backend, "victory_messages", DOCS, CHARS, log=quiet)
        self.assertEqual(rec["status"], "rejected")
        self.assertEqual(len(rec["attempts"]), MAX_REVISIONS + 1)
        self.assertEqual(backend.calls.count("generator-revision"), MAX_REVISIONS)
        self.assertTrue(rec["unresolved_issues"])

    def test_revision_prompt_carries_critic_feedback(self):
        issues = mechanics_guard(BAD_VICTORY)
        prompt = build_prompt(SPECS["victory_messages"], DOCS, CHARS, previous=BAD_VICTORY, issues=issues)
        self.assertIn("<critic_findings>", prompt)
        self.assertIn("submission", prompt)

    def test_rejected_content_never_written_to_outputs(self):
        backend = MockBackend([BAD_VICTORY] * 3)
        rec = run_output(backend, "victory_messages", DOCS, CHARS, log=quiet)
        with tempfile.TemporaryDirectory() as tmp:
            out, rej = Path(tmp) / "outputs", Path(tmp) / "outputs" / "rejected"
            path = export(rec, DOCS, backend, out, rej)
            self.assertEqual(path.parent, rej)
            self.assertFalse((out / "victory_messages.json").exists())
            body = json.loads(path.read_text(encoding="utf-8"))
            self.assertIn("REJECTED", body["provenance"]["review_status"])

    def test_fixture_draft_is_critiqued_not_generated(self):
        backend = MockBackend([GOOD_VICTORY])
        rec = run_output(backend, "victory_messages", DOCS, CHARS, initial_draft=BAD_VICTORY, log=quiet)
        self.assertEqual(backend.calls, ["critic", "generator-revision", "critic"])
        self.assertEqual(rec["status"], "approved")


if __name__ == "__main__":
    unittest.main()
