"""Unit tests for the reasoning layer. No LLM calls. Run:
    python -m unittest discover -s Tools/GoalAgent/tests
"""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HERE))

import goal_agent as ga  # noqa: E402

CONFIG = ga.load_config(HERE)
FEATURES = ga.load_features(HERE)
WEIGHTS = CONFIG["weights"]
POLICY = CONFIG["write_policy"]

GDD = """# Game
## Game Mechanics
### MVP Scope
- **Game Flow:** Start screen, character selection, match, and victory/defeat presentation.
### Controls
nothing here
### Inspector-Tunable Values
| Animation speed at each step of an animation | x |
"""
STYLE = """# Style
## UI
- chunky health bars and a large plain timer
## Music & Sounds
music
## Color Palette
#10151A
"""
ARCH = """# Arch
## 4. Seams
### 4.2 Combat → presentation
Juice's camera shake stays on a child offset.
### 4.3 Combat → UI
UI's `MatchFlow` stores the result.
```csharp
# not a heading
```
### 4.4 Character selection → match
"""


class FakeRepo:
    def __init__(self):
        self.root = Path(tempfile.mkdtemp())
        (self.root / "docs").mkdir()
        (self.root / "docs/GDD.md").write_text(GDD, encoding="utf-8")
        (self.root / "docs/STYLE_GUIDE.md").write_text(STYLE, encoding="utf-8")
        (self.root / "docs/ARCHITECTURE.md").write_text(ARCH, encoding="utf-8")
        (self.root / "Assets/Scenes").mkdir(parents=True)
        (self.root / "Assets/Scenes/Match.unity").write_text("x")
        (self.root / "ProjectSettings").mkdir()
        self.set_build_scenes(["Assets/Scenes/Match.unity"])

    def set_build_scenes(self, scenes):
        body = "EditorBuildSettings:\n  m_Scenes:\n" + "".join(
            f"  - enabled: 1\n    path: {s}\n    guid: 0\n" for s in scenes)
        (self.root / "ProjectSettings/EditorBuildSettings.asset").write_text(body)

    def add(self, rel, text="// file"):
        p = self.root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")

    def ranked(self):
        scan = ga.scan_codebase(self.root)
        statuses = ga.detect_gaps(FEATURES, scan, ga.DocLibrary(self.root))
        return statuses, ga.rank_gaps(statuses, scan, CONFIG)

    def cleanup(self):
        shutil.rmtree(self.root, ignore_errors=True)


class ParseSectionsTests(unittest.TestCase):
    def test_section_includes_subsections_and_skips_code_fences(self):
        s = ga.parse_sections(ARCH)
        self.assertIn("camera shake", s["4. seams"])
        self.assertIn("MatchFlow", s["4.3 combat → ui"])
        self.assertNotIn("not a heading", s)

    def test_requirement_found_and_not_found(self):
        repo = FakeRepo()
        try:
            docs = ga.DocLibrary(repo.root)
            req, ev = ga.check_requirement({"requirement": {"doc": "docs/GDD.md", "section": "MVP Scope", "match": "Start screen"}}, docs)
            self.assertTrue(req)
            self.assertIn("Start screen", ev)
            req, _ = ga.check_requirement({"requirement": {"doc": "docs/GDD.md", "section": "MVP Scope", "match": "tag team"}}, docs)
            self.assertFalse(req)
        finally:
            repo.cleanup()


class PriorityScoreTests(unittest.TestCase):
    def test_weighted_sum(self):
        f = {"ratings": {"impact": 3, "unblocks": 4, "testability": 4, "effort": 1}}
        score, breakdown = ga.priority_score(f, WEIGHTS)
        self.assertEqual(score, 3 * 3 + 2 * 4 + 2 * 4 - 1)
        self.assertEqual(breakdown["effort"], -1)

    def test_gated_scores_zero(self):
        f = {"ratings": {"impact": 5, "unblocks": 5, "testability": 5, "effort": 0}}
        self.assertEqual(ga.priority_score(f, WEIGHTS, "blocked: x")[0], 0.0)

    def test_effort_lowers_score(self):
        low = ga.priority_score({"ratings": {"impact": 3, "effort": 1}}, WEIGHTS)[0]
        high = ga.priority_score({"ratings": {"impact": 3, "effort": 4}}, WEIGHTS)[0]
        self.assertGreater(low, high)


class RankingTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()

    def tearDown(self):
        self.repo.cleanup()

    def test_empty_project_picks_start_screen_with_agreed_order(self):
        _, ranked = self.repo.ranked()
        actionable = [(r.goal.id, r.score) for r in ranked if r.actionable]
        self.assertEqual(actionable[0], ("start_screen", 24))
        # Tie at 23 broken by lower effort: match_end_handoff (1) before match_hud (2).
        self.assertEqual(actionable[1], ("match_end_handoff", 23))
        self.assertEqual(actionable[2], ("match_hud", 23))

    def test_gates(self):
        _, ranked = self.repo.ranked()
        gates = {r.goal.id: r.gate_reason for r in ranked}
        self.assertIn("start_screen", gates["character_select"])
        self.assertIn("match_end_handoff", gates["victory_screen"])
        self.assertIn("match_hud", gates["match_timer"])
        self.assertIn("Assets/Audio", gates["sound_effects"])
        self.assertTrue(gates["anim_speed_tuning"].startswith("unsafe"))

    def test_code_written_moves_goal_to_needs_integration_and_unblocks_dependent(self):
        self.repo.add("Assets/Scripts/UI/StartScreenController.cs", "public class StartScreenController {}")
        self.repo.add("Assets/UI/StartScreen.uxml", "<ui:UXML/>")
        statuses, ranked = self.repo.ranked()
        start = next(s for s in statuses if s.id == "start_screen")
        self.assertEqual(start.status, ga.STATUS_NEEDS_INTEGRATION)
        self.assertNotIn("start_screen", [r.goal.id for r in ranked])
        cs = next(r for r in ranked if r.goal.id == "character_select")
        self.assertIsNone(cs.gate_reason)

    GUID = "0123456789abcdef0123456789abcdef"

    def _start_screen_code_and_scene(self, scene_text):
        self.repo.add("Assets/Scripts/UI/StartScreenController.cs", "public sealed class StartScreenController {}")
        self.repo.add("Assets/Scripts/UI/StartScreenController.cs.meta", f"fileFormatVersion: 2\nguid: {self.GUID}\n")
        self.repo.add("Assets/UI/StartScreen.uxml")
        self.repo.add("Assets/Scripts/UI/RuralRumble.UI.asmdef", "{}")
        self.repo.add("Assets/Scenes/Start.unity", scene_text)
        self.repo.set_build_scenes(["Assets/Scenes/Start.unity", "Assets/Scenes/Match.unity"])

    def test_fully_integrated_goal_is_done(self):
        self._start_screen_code_and_scene(f"  m_Script: {{fileID: 11500000, guid: {self.GUID}, type: 3}}\n")
        statuses, _ = self.repo.ranked()
        self.assertEqual(next(s for s in statuses if s.id == "start_screen").status, ga.STATUS_DONE)

    def test_scene_without_component_needs_integration(self):
        self._start_screen_code_and_scene("--- !u!1 &1\nGameObject:\n  m_Name: Empty\n")
        statuses, _ = self.repo.ranked()
        start = next(s for s in statuses if s.id == "start_screen")
        self.assertEqual(start.status, ga.STATUS_NEEDS_INTEGRATION)
        self.assertEqual(start.failed_integration,
                         ["component_in_scene:Assets/Scenes/Start.unity:StartScreenController"])

    def test_assets_present_lifts_sound_gate(self):
        self.repo.add("Assets/Audio/hit.wav", "x")
        _, ranked = self.repo.ranked()
        self.assertIsNone(next(r for r in ranked if r.goal.id == "sound_effects").gate_reason)


class WritePolicyTests(unittest.TestCase):
    def setUp(self):
        self.repo = FakeRepo()

    def tearDown(self):
        self.repo.cleanup()

    def ok(self, rel):
        return ga.check_write_path(rel, POLICY, self.repo.root)[0]

    def test_allowed(self):
        self.assertTrue(self.ok("Assets/Scripts/UI/StartScreenController.cs"))
        self.assertTrue(self.ok("Assets/UI/StartScreen.uss"))
        self.assertTrue(self.ok("Assets/Tests/EditMode/UI/StartScreenFlowTests.cs"))

    def test_forbidden_wins(self):
        for rel in ["Assets/Scripts/Combat/CombatWrestler.cs", "Assets/Scripts/UI/RuralRumble.UI.asmdef",
                    "Assets/UI/Start.unity", "Assets/Art/UI/logo.png", "Assets/Animation/Barry/x.anim",
                    "Assets/Scripts/UI/x.cs.meta", "Assets/Tests/EditMode/Combat/X.cs", "../evil.cs",
                    "C:/Windows/x.cs", "Assets/Scripts/UI/../Combat/X.cs"]:
            self.assertFalse(self.ok(rel), rel)

    def test_never_overwrites(self):
        self.repo.add("Assets/UI/StartScreen.uxml")
        ok, reason = ga.check_write_path("Assets/UI/StartScreen.uxml", POLICY, self.repo.root)
        self.assertFalse(ok)
        self.assertIn("exists", reason)


class StageApplyTests(unittest.TestCase):
    def test_stage_rejects_bad_paths_and_apply_writes_only_approved(self):
        repo = FakeRepo()
        staging = repo.root / "staging"
        try:
            goal = ga.GoalStatus(FEATURES[0], True, "", ga.STATUS_MISSING, [], [])
            proposal = {"files": [
                {"path": "Assets/Scripts/UI/A.cs", "purpose": "a", "content": "class A {}"},
                {"path": "Assets/UI/B.uss", "purpose": "b", "content": ".b {}"},
                {"path": "Assets/Scripts/Combat/Hack.cs", "purpose": "bad", "content": "x"},
            ], "integration_notes": [], "assumptions": []}
            manifest = ga.stage(goal, proposal, CONFIG, repo.root, staging)
            statuses = {e["path"]: e["status"] for e in manifest["files"]}
            self.assertEqual(statuses["Assets/Scripts/Combat/Hack.cs"], "rejected_by_policy")
            self.assertFalse((staging / goal.id / "files/Assets/Scripts/Combat/Hack.cs").exists())

            ga.apply_staged(goal.id, CONFIG, repo.root, staging, approve=["Assets/Scripts/UI/A.cs"],
                            reject=["Assets/UI/B.uss"])
            self.assertTrue((repo.root / "Assets/Scripts/UI/A.cs").exists())
            self.assertFalse((repo.root / "Assets/UI/B.uss").exists())
            final = json.loads((staging / goal.id / "manifest.json").read_text())
            self.assertEqual({e["path"]: e["status"] for e in final["files"]}["Assets/UI/B.uss"], "rejected")
        finally:
            repo.cleanup()


if __name__ == "__main__":
    unittest.main()
