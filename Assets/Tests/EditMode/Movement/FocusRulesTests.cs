using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-037 / COMBAT_SPEC "Focus (D-037)", pure rules (<see cref="FocusRules"/>):
    /// - "a wrestler who is free to act faces the opponent left/right ... If the two are exactly vertically
    ///   aligned, the last left/right facing is kept."
    /// - "if the opponent's feet are more than focusBackStanceHeightPx higher on screen, the wrestler uses the
    ///   back stance; otherwise the front stance. Equal height: both front. This holds whichever side each
    ///   wrestler is on."
    /// - Run: "the left/right direction held when Run is pressed, else toward the opponent".
    /// - Tuning is in pixels, 60 px = 1 world unit (D-013, D-037).
    /// The threshold is read from a CombatTuning instance; 60 px/unit is the documented constant.
    /// </summary>
    public class FocusRulesTests
    {
        private const float PxPerUnit = 60f; // D-013 / D-037: 60 px = 1 world unit.

        private CombatTuning tuning;
        private float threshold;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<CombatTuning>();
            threshold = FocusRules.BackStanceHeightUnits(tuning);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        // COMBAT_SPEC Focus: faces the opponent left/right, from either side, whatever the current facing.
        [TestCase(1)]
        [TestCase(-1)]
        public void FacingToward_OpponentRight_FacesRight_OpponentLeft_FacesLeft(int current)
        {
            var self = new Vector2(10f, -12f);
            Assert.AreEqual(new Vector2Int(1, 0), FocusRules.FacingToward(self, new Vector2(12f, -12f), current, threshold));
            Assert.AreEqual(new Vector2Int(-1, 0), FocusRules.FacingToward(self, new Vector2(8f, -12f), current, threshold));
            // A tiny offset is still a side.
            Assert.AreEqual(new Vector2Int(1, 0), FocusRules.FacingToward(self, new Vector2(10.001f, -12f), current, threshold));
            Assert.AreEqual(new Vector2Int(-1, 0), FocusRules.FacingToward(self, new Vector2(9.999f, -12f), current, threshold));
        }

        // COMBAT_SPEC Focus: "exactly vertically aligned, the last left/right facing is kept" (both values).
        [TestCase(1)]
        [TestCase(-1)]
        public void FacingToward_EqualX_KeepsCurrentHorizontal(int current)
        {
            var self = new Vector2(10f, -12f);
            Assert.AreEqual(current, FocusRules.FacingToward(self, new Vector2(10f, -12f), current, threshold).x, "same spot");
            Assert.AreEqual(current, FocusRules.FacingToward(self, new Vector2(10f, -13f), current, threshold).x, "opponent below");
            Assert.AreEqual(current, FocusRules.FacingToward(self, new Vector2(10f, -12f + threshold + 0.5f), current, threshold).x, "opponent far above");
        }

        // COMBAT_SPEC Focus stance: "more than" the threshold -> back; exactly the threshold -> front.
        // Mirrored: holds whichever side the opponent is on.
        [TestCase(1)]
        [TestCase(-1)]
        public void FacingToward_StanceThresholdBoundary(int side)
        {
            // self.y = 0 so opponent.y - self.y is exactly the threshold (no float rounding at the boundary).
            var self = new Vector2(10f, 0f);
            float ox = self.x + side * 2f;
            const float eps = 1e-3f;

            Vector2Int atThreshold = FocusRules.FacingToward(self, new Vector2(ox, self.y + threshold), -side, threshold);
            Assert.AreEqual(new Vector2Int(side, 0), atThreshold, "exactly the threshold higher: front");

            Vector2Int justOver = FocusRules.FacingToward(self, new Vector2(ox, self.y + threshold + eps), -side, threshold);
            Assert.AreEqual(new Vector2Int(side, 1), justOver, "just over the threshold higher: back");

            Vector2Int justUnder = FocusRules.FacingToward(self, new Vector2(ox, self.y + threshold - eps), -side, threshold);
            Assert.AreEqual(new Vector2Int(side, 0), justUnder, "just under the threshold: front");

            Vector2Int equalHeight = FocusRules.FacingToward(self, new Vector2(ox, self.y), -side, threshold);
            Assert.AreEqual(new Vector2Int(side, 0), equalHeight, "equal height: front");
        }

        // COMBAT_SPEC Focus: the lower wrestler (smaller world y, nearer the camera) takes the back stance toward a
        // much higher opponent, while the higher wrestler stays front; a lower opponent never gives the back stance.
        [TestCase(1)]
        [TestCase(-1)]
        public void FacingToward_LowerOpponent_IsFront_AndHigherSeesLowerAsFront(int side)
        {
            var low = new Vector2(10f, -13.5f);
            var high = new Vector2(10f + side * 3f, low.y + threshold + 0.5f);
            Assert.AreEqual(new Vector2Int(side, 1), FocusRules.FacingToward(low, high, -side, threshold), "low wrestler: opponent higher -> back");
            Assert.AreEqual(new Vector2Int(-side, 0), FocusRules.FacingToward(high, low, side, threshold), "high wrestler: opponent lower -> front");
            Assert.AreEqual(0, FocusRules.FacingToward(high, new Vector2(high.x - side, high.y - 100f), side, threshold).y, "far lower: front");
        }

        // The threshold is a parameter, not a constant: a zero threshold makes any higher opponent "back".
        [Test]
        public void FacingToward_UsesThePassedThreshold()
        {
            var self = new Vector2(0f, 0f);
            Assert.AreEqual(1, FocusRules.FacingToward(self, new Vector2(1f, 0.01f), 1, 0f).y);
            Assert.AreEqual(0, FocusRules.FacingToward(self, new Vector2(1f, 0.01f), 1, 0.02f).y);
            Assert.AreEqual(0, FocusRules.FacingToward(self, new Vector2(1f, 0f), 1, 0f).y, "equal height is never back");
        }

        // CONTROLS Run (D-037): the held left/right wins over the opponent's side.
        [TestCase(1)]
        [TestCase(-1)]
        public void RunDirection_HeldLeftOrRight_GoesThatWay_WhereverTheOpponentIs(int held)
        {
            float dz = tuning.moveDeadzone;
            float moveX = held * Mathf.Min(1f, dz + 0.05f);
            Assert.AreEqual(held, FocusRules.RunDirection(moveX, dz, 5f, 1), "opponent right");
            Assert.AreEqual(held, FocusRules.RunDirection(moveX, dz, -5f, -1), "opponent left");
            Assert.AreEqual(held, FocusRules.RunDirection(held * 1f, dz, -held * 5f, -held), "full stick away from the opponent");
        }

        // CONTROLS Run (D-037): no left/right held (within the deadzone, inclusive) -> toward the opponent.
        [TestCase(5f, 1)]
        [TestCase(-5f, -1)]
        public void RunDirection_NothingHeldOrWithinDeadzone_GoesTowardOpponent(float dx, int expected)
        {
            float dz = tuning.moveDeadzone;
            foreach (float moveX in new[] { 0f, dz * 0.5f, -dz * 0.5f, dz, -dz })
            {
                Assert.AreEqual(expected, FocusRules.RunDirection(moveX, dz, dx, 1), $"moveX {moveX}, current +1");
                Assert.AreEqual(expected, FocusRules.RunDirection(moveX, dz, dx, -1), $"moveX {moveX}, current -1");
            }
        }

        // D-037 + Focus "exactly vertically aligned keeps the last facing": dx == 0 and nothing held -> current facing.
        [TestCase(1)]
        [TestCase(-1)]
        public void RunDirection_NothingHeld_EqualX_KeepsCurrentFacing(int current)
        {
            Assert.AreEqual(current, FocusRules.RunDirection(0f, tuning.moveDeadzone, 0f, current));
        }

        // D-037 tunable: pixels in tuning, 60 px = 1 unit.
        [TestCase(100f)]
        [TestCase(60f)]
        [TestCase(0f)]
        [TestCase(150f)]
        public void BackStanceHeightUnits_IsPixelsOver60(float px)
        {
            tuning.focusBackStanceHeightPx = px;
            Assert.AreEqual(px / PxPerUnit, FocusRules.BackStanceHeightUnits(tuning), 1e-6f);
        }

        [Test]
        public void MatchCamera_PixelsPerUnit_Is60()
        {
            Assert.AreEqual(PxPerUnit, MatchCamera.PixelsPerUnit, 0f, "D-013: 60 px = 1 world unit");
        }

        // COMBAT_SPEC Tuning Values, Focus: "Focus back-stance height | 50 px | Set".
        [Test]
        public void NewTuning_FocusBackStanceHeight_Is100Px()
        {
            Assert.AreEqual(50f, tuning.focusBackStanceHeightPx, 1e-5f);
        }
    }
}
