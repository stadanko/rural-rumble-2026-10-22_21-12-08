using NUnit.Framework;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-020 / COMBAT_SPEC Running / CONTROLS.md Running: one press of Run (B / Space) starts a run in the
    /// facing left/right direction; it continues until an obstacle or the arena edge (hits and other
    /// actions are outside the movement slice). Facing up or down runs toward the last left/right
    /// facing. Running is left or right only (COMBAT_SPEC: "one of two directions").
    /// </summary>
    public class RunningTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void Run_OnePressFromStanding_RunsInFacingDirectionWithNothingHeld()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor); // faces right (CPU is to the right)
            Assert.AreEqual(1, h.Player.HorizontalFacing);
            h.PlayerInput.PressRunOnce();
            h.Tick(10); // one press, then nine ticks with no input at all
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(SliceArena.OpenFloor.x + h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(SliceArena.OpenFloor.y, h.Player.Position.y, 1e-6f);
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing);
        }

        [Test]
        public void Run_FromWalkingLeft_RunsLeft()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(3);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            float x0 = h.Player.Position.x;
            h.PlayerInput.Release();
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(x0 - h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
        }

        // D-020: facing up or down, the run goes toward the last left/right facing.
        [TestCase(-1f, 0f, 1f, -1)]   // left, then up    -> runs left
        [TestCase(-1f, 0f, -1f, -1)]  // left, then down  -> runs left
        [TestCase(1f, 0f, 1f, 1)]     // right, then up   -> runs right
        [TestCase(1f, 0f, -1f, 1)]    // right, then down -> runs right
        public void Run_FacingStraightUpOrDown_UsesLastHorizontalFacing(float firstX, float firstY, float thenY, int expectedDir)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(firstX, firstY);
            h.Tick(2);
            h.PlayerInput.HeldMove = new Vector2(0f, thenY);
            h.Tick(2);
            Assert.AreEqual(new Vector2(0f, thenY), h.Player.Facing, "precondition: facing straight up/down");
            h.PlayerInput.Release();
            float x0 = h.Player.Position.x;
            float y0 = h.Player.Position.y;
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(x0 + expectedDir * h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(y0, h.Player.Position.y, 1e-6f, "a run never moves in depth");
        }

        // D-020 "facing direction (left or right)": a diagonal facing runs along its horizontal part.
        [TestCase(-1f, 1f, -1)]
        [TestCase(-1f, -1f, -1)]
        [TestCase(1f, 1f, 1)]
        public void Run_FacingDiagonal_RunsHorizontally(float x, float y, int expectedDir)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(2);
            h.PlayerInput.Release();
            Vector2 p0 = h.Player.Position;
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            Assert.AreEqual(p0.x + expectedDir * h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(p0.y, h.Player.Position.y, 1e-6f);
        }

        // COMBAT_SPEC Running: left or right only. Holding a depth direction does not steer the run.
        [Test]
        public void Run_HoldingUpOrDown_DoesNotSteerDepth()
        {
            using var h = new MatchHarness(new Vector2(9f, -12f));
            h.PlayerInput.PressRunOnce();
            h.PlayerInput.HeldMove = Vector2.up;
            h.Tick(10);
            h.PlayerInput.HeldMove = new Vector2(1f, -1f);
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(-12f, h.Player.Position.y, 1e-6f);
            Assert.AreEqual(9f + h.Tuning.runSpeed * 20 * MatchHarness.Dt, h.Player.Position.x, Tol);
        }

        // D-020: the run continues until the arena edge, then the wrestler returns to Standing.
        [Test]
        public void Run_StopsAtRightEdge_ReturnsToStanding()
        {
            using var h = new MatchHarness(new Vector2(20f, -13.5f), cpuStart: new Vector2(26f, -11f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(SliceArena.BandXMax - h.Tuning.feetHalfWidth, h.Player.Position.x, Tol);
        }

        [Test]
        public void Run_StopsAtLeftEdge_ReturnsToStanding()
        {
            using var h = new MatchHarness(new Vector2(6f, -12f), cpuStart: new Vector2(1f, -13f)); // faces left
            Assert.AreEqual(-1, h.Player.HorizontalFacing);
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(SliceArena.BandXMin + h.Tuning.feetHalfWidth, h.Player.Position.x, Tol);
        }

        // D-020: the run stops at an obstacle.
        [Test]
        public void Run_StopsAtTruck_ReturnsToStanding()
        {
            using var h = new MatchHarness(new Vector2(14f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("running into truck"));
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            float face = SliceArena.Truck.XMax + h.Tuning.feetHalfWidth;
            Assert.That(h.Player.Position.x, Is.InRange(face, face + 0.01f));
        }

        [Test]
        public void Run_StopsAtPool_ReturnsToStanding()
        {
            using var h = new MatchHarness(new Vector2(10f, SliceArena.ObstacleLaneY));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("running into pool"));
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            float face = SliceArena.Pool.XMin - h.Tuning.feetHalfWidth;
            Assert.That(h.Player.Position.x, Is.InRange(face - 0.01f, face));
        }

        [Test]
        public void Run_StartedAgainstEdge_EndsStandingSameTick()
        {
            using var h = new MatchHarness(new Vector2(2f, -12f), cpuStart: new Vector2(20f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(1f);
            h.PlayerInput.Release();
            h.PlayerInput.PressRunOnce();
            h.Tick(1);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
        }

        // After a run ends, the wrestler walks normally again (no stuck state).
        [Test]
        public void Run_AfterStopping_CanWalkAndRunAgain()
        {
            using var h = new MatchHarness(new Vector2(20f, -12f), cpuStart: new Vector2(26f, -13f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(5);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            h.PlayerInput.Release();
            h.PlayerInput.PressRunOnce();
            h.Tick(5);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(-1, h.Player.HorizontalFacing);
        }
    }
}
