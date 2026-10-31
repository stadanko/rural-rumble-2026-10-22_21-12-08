using NUnit.Framework;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// COMBAT_SPEC State Definitions: Standing = not moving; Walking = walking in one of eight directions.
    /// Speeds come from CombatTuning (walkSpeedHorizontal / walkSpeedDepth, ARCHITECTURE 5).
    /// Bounds: ARCHITECTURE 3.1 walkable feet band, trailer back wall = band top edge.
    /// </summary>
    public class WalkingTests
    {
        private const float Tol = 1e-3f;

        // COMBAT_SPEC: Walking. Horizontal axis moves at walkSpeedHorizontal.
        [TestCase(1f, 1)]
        [TestCase(-1f, -1)]
        public void Walk_Horizontal_MovesAtHorizontalSpeed(float stickX, int sign)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(stickX, 0f);
            h.Tick(25);
            float expected = sign * h.Tuning.walkSpeedHorizontal * 25 * MatchHarness.Dt;
            Assert.AreEqual(SliceArena.OpenFloor.x + expected, h.Player.Position.x, Tol);
            Assert.AreEqual(SliceArena.OpenFloor.y, h.Player.Position.y, Tol);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
        }

        // COMBAT_SPEC: Walking. Depth axis (up = toward the trailer = +y world) moves at walkSpeedDepth.
        [TestCase(1f, 1)]
        [TestCase(-1f, -1)]
        public void Walk_Depth_MovesAtDepthSpeed(float stickY, int sign)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(0f, stickY);
            h.Tick(25);
            float expected = sign * h.Tuning.walkSpeedDepth * 25 * MatchHarness.Dt;
            Assert.AreEqual(SliceArena.OpenFloor.x, h.Player.Position.x, Tol);
            Assert.AreEqual(SliceArena.OpenFloor.y + expected, h.Player.Position.y, Tol);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
        }

        // D-029: diagonals keep the horizontal:vertical direction of travel and move at walkSpeedDiagonal.
        // (Updated in combat-core step 2: the movement slice moved each axis at its own speed.)
        [Test]
        public void Walk_Diagonal_UsesDiagonalSpeedAlongAxisRatio()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(-1f, -1f);
            h.Tick(25);
            float t = 25 * MatchHarness.Dt;
            Vector2 dir = new Vector2(-h.Tuning.walkSpeedHorizontal, -h.Tuning.walkSpeedDepth).normalized;
            V2Assert.AreEqual(SliceArena.OpenFloor + dir * h.Tuning.walkSpeedDiagonal * t, h.Player.Position, Tol);
        }

        [Test]
        public void Walk_Diagonal_SpeedIsTunable()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.Tuning.walkSpeedDiagonal = 1.5f;
            h.PlayerInput.HeldMove = new Vector2(1f, 1f);
            h.Tick(10);
            float travelled = Vector2.Distance(SliceArena.OpenFloor, h.Player.Position);
            Assert.AreEqual(1.5f * 10 * MatchHarness.Dt, travelled, Tol);
        }

        // COMBAT_SPEC: Standing = not moving. Releasing the stick stops at once and returns to Standing.
        [Test]
        public void Walk_Release_ReturnsToStandingAndStops()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(10);
            h.PlayerInput.Release();
            h.Tick(1);
            Vector2 stopped = h.Player.Position;
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            h.Tick(30);
            V2Assert.AreEqual(stopped, h.Player.Position, 1e-6f);
        }

        // ARCHITECTURE 3.1: feet stay inside the walkable band on all four sides (box half-extents from tuning).
        [Test]
        public void Walk_ClampsToLeftEdge()
        {
            using var h = new MatchHarness(new Vector2(2f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(3f);
            Assert.AreEqual(SliceArena.BandXMin + h.Tuning.feetHalfWidth, h.Player.Position.x, Tol);
        }

        [Test]
        public void Walk_ClampsToRightEdge()
        {
            using var h = new MatchHarness(new Vector2(24f, -12f), cpuStart: new Vector2(1f, -13f));
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(3f);
            Assert.AreEqual(SliceArena.BandXMax - h.Tuning.feetHalfWidth, h.Player.Position.x, Tol);
        }

        [Test]
        public void Walk_ClampsToFrontEdge()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.down;
            h.TickSeconds(3f);
            Assert.AreEqual(SliceArena.BandYMin + h.Tuning.feetHalfDepth, h.Player.Position.y, Tol);
        }

        // ARCHITECTURE 3.1: the trailer is the back wall along row 580 (band top). x = 13 is clear of truck and pool.
        [Test]
        public void Walk_ClampsToBackWall()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(3f);
            Assert.AreEqual(SliceArena.BandYMax - h.Tuning.feetHalfDepth, h.Player.Position.y, Tol);
        }

        [Test]
        public void Walk_DiagonalIntoCorner_ClampsBothAxes()
        {
            using var h = new MatchHarness(new Vector2(3f, -12f));
            h.PlayerInput.HeldMove = new Vector2(-1f, -1f);
            h.TickSeconds(3f);
            V2Assert.AreEqual(new Vector2(SliceArena.BandXMin + h.Tuning.feetHalfWidth, SliceArena.BandYMin + h.Tuning.feetHalfDepth),
                h.Player.Position, Tol);
        }

        // Not in COMBAT_SPEC (spec gap): implementers chose that pushing into a wall stays Walking.
        [Test]
        public void Walk_PushingIntoWall_StaysWalkingInPlace()
        {
            using var h = new MatchHarness(new Vector2(1f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(1f);
            Vector2 atWall = h.Player.Position;
            h.Tick(10);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            V2Assert.AreEqual(atWall, h.Player.Position, 1e-6f);
        }

        // MatchHarness starts are clamped into the band (CombatMatch places wrestlers inside the bounds).
        [Test]
        public void Match_StartOutsideBand_IsClampedInside()
        {
            using var h = new MatchHarness(new Vector2(-5f, -20f));
            V2Assert.AreEqual(new Vector2(SliceArena.BandXMin + h.Tuning.feetHalfWidth, SliceArena.BandYMin + h.Tuning.feetHalfDepth),
                h.Player.Position, Tol);
        }
    }
}
