using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 4.2 IWrestlerView: Facing = last 8-way direction; HorizontalFacing = last left/right
    /// (+1 right, -1 left), kept while Facing is straight up/down.
    /// STYLE_GUIDE animations: back-facing walk for Up/Left, Up, Up/Right; front-facing for the other five.
    /// "When user stops walking, they will stand to match the most recent animation" (Standing keeps facing).
    /// </summary>
    public class FacingTests
    {
        private static readonly object[] EightWay =
        {
            new object[] { 1f, 0f, false }, new object[] { 1f, 1f, true }, new object[] { 0f, 1f, true },
            new object[] { -1f, 1f, true }, new object[] { -1f, 0f, false }, new object[] { -1f, -1f, false },
            new object[] { 0f, -1f, false }, new object[] { 1f, -1f, false },
        };

        [TestCaseSource(nameof(EightWay))]
        public void Walk_SetsFacingToUnitEightWayDirection_AndBackFacingForUpwardDirections(float x, float y, bool back)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(2);
            V2Assert.AreEqual(new Vector2(x, y).normalized, h.Player.Facing, 1e-4f);
            Assert.AreEqual(back, h.Player.Facing.y > 0f, "IWrestlerView back-facing rule (Facing.y > 0)");
            Assert.AreEqual(back, ((CombatWrestler)h.Player).IsBackFacing);
        }

        [TestCase(1f, 1f, 1)]
        [TestCase(1f, -1f, 1)]
        [TestCase(-1f, 1f, -1)]
        [TestCase(-1f, -1f, -1)]
        public void Walk_DiagonalSetsHorizontalFacing(float x, float y, int expected)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor, cpuStart: expected > 0 ? new Vector2(1f, -13f) : MatchHarness.DefaultCpuStart);
            Assert.AreEqual(-expected, h.Player.HorizontalFacing, "precondition: starts facing the other way");
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(1);
            Assert.AreEqual(expected, h.Player.HorizontalFacing);
        }

        [TestCase(-1, 1f)]
        [TestCase(-1, -1f)]
        [TestCase(1, 1f)]
        [TestCase(1, -1f)]
        public void Walk_StraightUpOrDown_KeepsLastHorizontalFacing(int lastHorizontal, float y)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(lastHorizontal, 0f);
            h.Tick(1);
            h.PlayerInput.HeldMove = new Vector2(0f, y);
            h.Tick(5);
            Assert.AreEqual(lastHorizontal, h.Player.HorizontalFacing);
        }

        [TestCaseSource(nameof(EightWay))]
        public void Standing_KeepsLastFacing(float x, float y, bool back)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(3);
            Vector2 facing = h.Player.Facing;
            int hf = h.Player.HorizontalFacing;
            h.PlayerInput.Release();
            h.Tick(20);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(facing, h.Player.Facing);
            Assert.AreEqual(hf, h.Player.HorizontalFacing);
            Assert.AreEqual(back, h.Player.Facing.y > 0f);
        }

        // Start of match: each wrestler faces the other (CombatMatch constructor; not stated in the spec).
        [Test]
        public void MatchStart_WrestlersFaceEachOther()
        {
            using var h = new MatchHarness(new Vector2(9f, -11.6667f), cpuStart: new Vector2(17.6667f, -11.6667f));
            Assert.AreEqual(1, h.Player.HorizontalFacing);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing);
            Assert.AreEqual(new Vector2(-1f, 0f), h.Cpu.Facing);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }
    }
}
