"""Offline tests for the Style Guide Agent. No model calls: a scripted fake stands in for claude -p.

    python -m unittest discover -s Tools/StyleAgent/tests -v
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))

import checks  # noqa: E402
import loop  # noqa: E402
import rules  # noqa: E402

GOOD_TRAILER = {
    "subject": "Run-down single-wide trailer, back row of the Duckville arena",
    "prompt": ("16-bit pixel art sprite in the style of Final Fight and WrestleFest arcade backgrounds: a rusted, "
               "sagging single-wide trailer with faded siding, a patched tarp roof, a bent rooftop TV antenna and a "
               "cracked satellite dish, weeds around the cinder blocks. Late-afternoon golden hour, warm orange light, "
               "long shadows. Palette #10151A #25372C #795037 #A74727 #D26938 #E19A36 #FFD65A #E6DFCC."),
    "negative_prompt": "mountains, hills, city, neon, 3D, photorealistic, smooth gradients",
    "palette": ["#10151A", "#25372C", "#795037", "#A74727", "#D26938", "#E19A36", "#FFD65A", "#E6DFCC"],
    "lighting": "Late-afternoon golden hour, warm orange glow, long shadows across the dirt",
    "details": "Rust streaks under the windows, a broken porch step, mildew on the skirting.",
    "size_px": {"width": 900, "height": 470},
    "scale_note": "470 px tall, about twice Barry's 240-px standing sprite.",
    "layer": "background",
}
GOOD_TALK = {"lines": [
    {"speaker": "barry", "text": "Pick up them cigarette butts off my gravel, boy, or I'll Irish Whip you into Duckville."},
    {"speaker": "peaux", "text": "Big talk from a mustache wearing a beer gut, Hot Potato."},
    {"speaker": "barry", "text": "I hauled freight coast to coast. I can haul your skinny goth butt off this trailer lot."},
    {"speaker": "peaux", "text": "Smear your eyeliner, old man. You'll be napping in the dirt by sundown."},
]}


def load_fixture(name):
    return json.loads((HERE / "fixtures" / name).read_text(encoding="utf-8"))


class FakeLLM:
    """Scripted stand-in for ClaudeCliBackend. The fake Evaluator scores each rule with the
    deterministic check (or 9 when a rule has none), so loop behaviour is predictable."""

    def __init__(self, task, rules_, ctx, refine_drafts, llm_score=9):
        self.task, self.rules, self.ctx = task, rules_, ctx
        self.refine_drafts = list(refine_drafts)
        self.llm_score = llm_score
        self.prompts = []

    def complete_json(self, role, system, user, schema):
        self.prompts.append((role, user))
        meta = {"role": role, "cost_usd": 0}
        if role == "evaluator":
            draft = json.loads(user.split("<draft>\n", 1)[1].split("\n</draft>", 1)[0])
            chk = checks.run_checks(draft, self.task, self.rules, self.ctx)
            scores = [{"rule_id": rid, "score": chk[rid]["score"] if rid in chk else self.llm_score,
                       "reason": "fake", "evidence": "", "fix": "fake fix"} for rid in self.task["rules"]]
            return {"scores": scores, "summary": "fake"}, meta
        if role == "refiner":
            return {"draft": self.refine_drafts.pop(0), "changes": [{"rule_id": "X", "change": "fake"}]}, meta
        raise AssertionError(role)


class Base(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rules = rules.load_rules()
        cls.tasks, cls.doc_sections = rules.load_tasks(cls.rules)
        narrative = rules.doc_pack(cls.doc_sections, "narrative")
        cls.ctx = {"palette": rules.palette_from_docs(), "characters": narrative.characters(),
                   "doc_numbers": checks.doc_numbers(narrative.as_prompt())}

    def task(self, tid):
        return next(t for t in self.tasks if t["id"] == tid)


class RulebookTests(Base):
    def test_every_rule_quote_is_in_the_docs(self):
        results = rules.verify_rule_sources(list(self.rules.values()))
        self.assertGreaterEqual(len(results), 16)
        self.assertEqual([r for r in results if not r["found"]], [])

    def test_invented_rule_is_refused(self):
        data = json.loads(rules.RULES_PATH.read_text(encoding="utf-8"))
        data["rules"][0]["source"]["quotes"] = ["Wrestlers ride dinosaurs through a neon city."]
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "rules.json"
            p.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(rules.RuleError):
                rules.load_rules(path=p)

    def test_palette_comes_from_style_guide(self):
        self.assertEqual(len(self.ctx["palette"]), 14)
        self.assertIn("#FFD65A", self.ctx["palette"])

    def test_characters_come_from_gdd(self):
        self.assertEqual(set(self.ctx["characters"]), {"barry", "peaux"})


class CheckTests(Base):
    def test_planted_trailer_scores_low_with_reasons(self):
        res = checks.run_checks(load_fixture("off_style_trailer.json"), self.task("trailer_backdrop"), self.rules, self.ctx)
        self.assertTrue(all(r["score"] <= 2 for r in res.values()), res)
        self.assertIn("#00FFFF", res["A-02"]["reason"])
        self.assertIn("mountains", res["A-04"]["reason"])

    def test_good_trailer_scores_high(self):
        res = checks.run_checks(GOOD_TRAILER, self.task("trailer_backdrop"), self.rules, self.ctx)
        self.assertTrue(all(r["score"] >= 9 for r in res.values()), res)

    def test_negated_scenery_in_prompt_is_ignored(self):
        d = dict(GOOD_TRAILER, prompt=GOOD_TRAILER["prompt"] + " No mountains or city in sight.")
        self.assertEqual(checks.banned_scenery(d, self.task("trailer_backdrop"), self.ctx)["score"], 10)

    def test_negation_does_not_hide_moves_in_speech(self):
        d = {"lines": [{"speaker": "peaux", "text": "Not before my piledriver, old man."}]}
        self.assertLess(checks.mvp_moves(d, self.task("trash_talk_prematch"), self.ctx)["score"], 10)

    def test_planted_trash_talk_violations_found(self):
        res = checks.run_checks(load_fixture("off_style_trash_talk.json"), self.task("trash_talk_prematch"),
                                self.rules, self.ctx)
        self.assertEqual(res["N-01"]["score"], 0)   # Hulk Hogan, "The Hammer"
        self.assertEqual(res["N-02"]["score"], 0)   # pin, championship belt
        self.assertEqual(res["N-03"]["score"], 0)   # piledriver, steel chair
        self.assertLess(res["N-08"]["score"], 10)   # Barry speaks twice in a row

    def test_invented_numbers_flagged_per_speaker(self):
        d = {"lines": [{"speaker": "barry", "text": "Thirty years hauling freight, boy."},
                       {"speaker": "peaux", "text": "Expelled at sixteen. One Irish Whip, old man."}]}
        task = self.task("trash_talk_prematch")
        self.assertIn("Thirty", checks.CHECKERS["doc_facts_barry"](d, task, self.ctx)["reason"])
        self.assertEqual(checks.CHECKERS["doc_facts_peaux"](d, task, self.ctx)["score"], 10)   # sixteen is in the GDD

    def test_good_trash_talk_scores_high(self):
        res = checks.run_checks(GOOD_TALK, self.task("trash_talk_prematch"), self.rules, self.ctx)
        self.assertTrue(all(r["score"] >= 9 for r in res.values()), res)


class ScorecardTests(Base):
    def test_score_is_graded_not_binary_and_lower_score_wins(self):
        task = self.task("trash_talk_prematch")
        llm = {"scores": [{"rule_id": rid, "score": 9, "reason": "r", "evidence": "", "fix": ""} for rid in task["rules"]]}
        chk = {"N-08": {"score": 4, "reason": "too long", "evidence": []}}
        card = loop.build_scorecard(task, self.rules, llm, chk)
        n08 = next(r for r in card["rules"] if r["rule_id"] == "N-08")
        self.assertEqual(n08["score"], 4)
        self.assertIn("too long", n08["reason"])
        self.assertTrue(0 < card["total"] < 100)
        self.assertTrue(all(r["reason"] for r in card["rules"]))

    def test_hard_rule_floor_blocks_high_total(self):
        task = self.task("trash_talk_prematch")
        llm = {"scores": [{"rule_id": rid, "score": 10, "reason": "r", "evidence": "", "fix": ""} for rid in task["rules"]]}
        card = loop.build_scorecard(task, self.rules, llm, {"N-02": {"score": 5, "reason": "pin", "evidence": []}})
        self.assertGreaterEqual(card["total"], loop.THRESHOLD)
        self.assertFalse(card["passed"])
        self.assertEqual(card["hard_failures"], ["N-02"])

    def test_soft_floor_blocks_one_weak_rule(self):
        task = self.task("trash_talk_prematch")
        llm = {"scores": [{"rule_id": rid, "score": 10 if rid != "N-06" else 5, "reason": "r", "evidence": "", "fix": ""}
                          for rid in task["rules"]]}
        card = loop.build_scorecard(task, self.rules, llm, {})
        self.assertGreaterEqual(card["total"], loop.THRESHOLD)
        self.assertFalse(card["passed"])
        self.assertEqual(card["floor_failures"], ["N-06"])


class LoopTests(Base):
    def run_task(self, tid, seed, refine_drafts, max_ref=3, llm_score=9):
        task = self.task(tid)
        fake = FakeLLM(task, self.rules, self.ctx, refine_drafts, llm_score)
        res = loop.run_task(fake, task, self.rules, self.doc_sections, self.ctx, seed_draft=seed,
                            max_refinements=max_ref, log=lambda *a: None)
        return res, fake

    def test_planted_draft_is_fixed_without_help(self):
        res, fake = self.run_task("trash_talk_prematch", load_fixture("off_style_trash_talk.json"), [GOOD_TALK])
        self.assertEqual(res["status"], "approved")
        self.assertEqual(len(res["iterations"]), 2)
        self.assertLess(res["score_trajectory"][0], res["score_trajectory"][1])
        refine_prompt = next(u for role, u in fake.prompts if role == "refiner")
        self.assertIn("Hogan", refine_prompt)          # the Refiner is told what is wrong
        self.assertIn("piledriver", refine_prompt)

    def test_never_good_enough_is_rejected_after_max_refinements(self):
        bad = load_fixture("off_style_trailer.json")
        res, _ = self.run_task("trailer_backdrop", bad, [bad, bad], max_ref=2)
        self.assertEqual(res["status"], "rejected")
        self.assertEqual(len(res["iterations"]), 3)

    def test_refines_from_best_draft_after_regression(self):
        mid = dict(GOOD_TRAILER, size_px={"width": 900, "height": 300})   # too short: hard rule A-08 fails
        worse = load_fixture("off_style_trailer.json")
        res, fake = self.run_task("trailer_backdrop", mid, [worse, GOOD_TRAILER])
        self.assertEqual(res["status"], "approved")
        self.assertEqual(res["score_trajectory"][0] > res["score_trajectory"][1], True)   # revision 1 regressed
        self.assertEqual(res["iterations"][1]["refined_from_iteration"], 0)   # so it went back to the better draft
        second_refine = [u for role, u in fake.prompts if role == "refiner"][1]
        self.assertIn("height 300px", second_refine)
        self.assertNotIn("Photorealistic", second_refine)

    def test_export_marks_provenance_and_review(self):
        res, _ = self.run_task("trash_talk_prematch", load_fixture("off_style_trash_talk.json"), [GOOD_TALK])
        with tempfile.TemporaryDirectory() as d:
            old = loop.OUTPUTS
            loop.OUTPUTS = Path(d)
            try:
                data = json.loads(loop.export(res, "fake-model", "run").read_text(encoding="utf-8"))
            finally:
                loop.OUTPUTS = old
        self.assertEqual(data["provenance"]["origin"], "AI-generated")
        self.assertIn("pending designer review", data["provenance"]["review_status"])
        self.assertIn("N-02", data["style_score"]["rules"])

    def test_transcript_renders(self):
        res, _ = self.run_task("trash_talk_prematch", load_fixture("off_style_trash_talk.json"), [GOOD_TALK])
        md = loop.transcript_md({"run_id": "x", "model": "m", "threshold": 85, "hard_floor": 7,
                                 "max_refinements": 3, "tasks": [res]})
        self.assertIn("Iteration 1 (from refiner)", md)
        self.assertIn("| N-02 (hard)", md)


if __name__ == "__main__":
    unittest.main()
