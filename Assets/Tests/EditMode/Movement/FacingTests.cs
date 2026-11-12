using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 4.2 IWrestlerView: Facing is a unit 8-way vector, HorizontalFacing is +1 right / -1 left,
    /// back-facing = Facing.y > 0. Since D-037 (COMBAT_SPEC "Focus") the facing of a free wrestler comes from
    /// the two positions, not from input: "movement input never turns a wrestler or chooses the stance".
    /// The old input-driven rules (Facing = last walk direction, D-020 straight up/down keeps the last
    /// horizontal) are superseded; each old test below is replaced by its Focus equivalent.
    /// </summary>
    public class FacingTests
    {
        private static readonly object[] EightWay =
        {
            new object[] { 1f, 0f }, new object[] { 1f, 1f }, new object[] { 0f, 1f }, new object[] { -1f, 1f },
            new object[] { -1f, 0f }, new object[] { -1f, -1f }, new object[] { 0f, -1f }, new object[] { 1f, -1f },
        };

        // Replaces Walk_SetsFacingToUnitEightWayDirection_AndBackFacingForUpwardDirections.
        // COMBAT_SPEC Focus "Input: movement input never turns a wrestler or chooses the stance", on both sides.
        [TestCaseSource(nameof(EightWay))]
        public void Walk_AnyDirection_DoesNotTurn_KeepsFacingOpponent(float x, float y)
        {
            foreach (int side in new[] { 1, -1 })
            {
                // Opponent 6 units to the side and 0.5 lower: front stance, whatever 3 ticks of walking do.
                Vector2 p = SliceArena.OpenFloor;
                using var h = new MatchHarness(p, cpuStart: new Vector2(p.x + side * 6f, p.y - 0.5f));
                h.PlayerInput.HeldMove = new Vector2(x, y);
                h.Tick(3);
                Assert.AreEqual(CombatState.Walking, h.Player.State, $"side {side}");
                Assert.AreEqual(new Vector2(side, 0f), h.Player.Facing, $"side {side}: faces the opponent, front stance");
                Assert.AreEqual(side, h.Player.HorizontalFacing);
                Assert.IsFalse(h.PlayerW.IsBackFacing);
                Assert.IsFalse(h.FacingChanges.Exists(e => e.id == WrestlerId.Player), $"side {side}: walking never turns");
            }
        }

        // Replaces Walk_DiagonalSetsHorizontalFacing. COMBAT_SPEC Focus: "Moving away means backing up while
        // still facing the opponent" - walking diagonally away keeps HorizontalFacing toward the opponent.
        [TestCase(1f, 1f)]
        [TestCase(1f, -1f)]
        [TestCase(-1f, 1f)]
        [TestCase(-1f, -1f)]
        public void WalkDiagonally_AwayOrToward_HorizontalFacingStaysOnOpponent(float x, float y)
        {
            // Opponent on the far side of the open floor, 0.3 lower: front stance throughout the half-second walk.
            using var h = new MatchHarness(SliceArena.OpenFloor, cpuStart: new Vector2(SliceArena.OpenFloor.x + 10f, -13.3f));
            h.PlayerInput.HeldMove = new Vector2(x, y);
            for (int i = 0; i < 25; i++)
            {
                h.Tick(1);
                Assert.AreEqual(1, h.Player.HorizontalFacing, $"tick {i}");
            }
            Assert.AreNotEqual(SliceArena.OpenFloor.x, h.Player.Position.x, "precondition: the player moved");
        }

        // Replaces Walk_StraightUpOrDown_KeepsLastHorizontalFacing. COMBAT_SPEC Focus: "If the two are exactly
        // vertically aligned, the last left/right facing is kept" - here the starting facings (+1 / -1).
        [TestCase(1f)]
        [TestCase(-1f)]
        public void VerticallyAligned_KeepsLastHorizontalFacing_WhileWalkingUpOrDown(float y)
        {
            using var h = new MatchHarness(new Vector2(13f, -12.5f), cpuStart: new Vector2(13f, -11.5f));
            Assert.AreEqual(1, h.Player.HorizontalFacing, "match start: player +1");
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing, "match start: CPU -1");
            h.PlayerInput.HeldMove = new Vector2(0f, y);
            h.Tick(10);
            Assert.AreEqual(13f, h.Player.Position.x, 1e-6f, "precondition: still exactly aligned");
            Assert.AreEqual(1, h.Player.HorizontalFacing);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);
        }

        // Replaces Standing_KeepsLastFacing. STYLE_GUIDE "stand to match the most recent animation" now meets
        // Focus: after walking any direction and stopping, the Standing wrestler still faces the opponent.
        [TestCaseSource(nameof(EightWay))]
        public void Standing_AfterWalkingAnyDirection_FacesOpponent(float x, float y)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor); // CPU right and lower: (1, 0)
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(3);
            h.PlayerInput.Release();
            h.Tick(20);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing);
            Assert.AreEqual(1, h.Player.HorizontalFacing);
        }

        // Start of match: each wrestler faces the other (D-037: CombatMatch applies Focus at construction).
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
