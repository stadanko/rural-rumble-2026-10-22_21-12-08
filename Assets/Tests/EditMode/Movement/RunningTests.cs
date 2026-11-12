using NUnit.Framework;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-020 / COMBAT_SPEC Running / CONTROLS.md Running: one press of Run (B / Space) starts a run; it
    /// continues until an obstacle or the arena edge (hits, strikes and Run-cancel are covered in
    /// Combat/RunRulesTests). Running is left or right only (COMBAT_SPEC: "one of two directions").
    /// Run direction since D-037 (CONTROLS Run, COMBAT_SPEC Focus): "the left/right direction held on the Left
    /// Stick/D-Pad when Run is pressed; with no left/right held, toward the opponent" (this replaces D-020's
    /// "facing direction; facing up/down uses the last left/right facing"). The runner faces the run direction.
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

        // D-037: held left on the Run press runs left, even away from the opponent (CPU is to the right).
        [Test]
        public void Run_FromWalkingLeft_HoldingLeft_RunsLeft()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(3);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "precondition: backing away, still facing the CPU");
            float x0 = h.Player.Position.x;
            h.PlayerInput.PressRunOnce(); // left still held on the press
            h.PlayerInput.Release();
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(x0 - h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(new Vector2(-1f, 0f), h.Player.Facing, "the runner faces the run direction");
        }

        // D-037: walking left then releasing before Run = nothing held -> toward the opponent (right).
        [Test]
        public void Run_FromWalkingLeft_Released_RunsTowardOpponent()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(3);
            float x0 = h.Player.Position.x;
            h.PlayerInput.Release();
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(x0 + h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
        }

        // Replaces Run_FacingStraightUpOrDown_UsesLastHorizontalFacing (D-020). D-037: holding straight up or
        // down (no left/right) on the Run press runs toward the opponent, on either side; the run never moves in depth.
        [TestCase(1, 1f)]
        [TestCase(1, -1f)]
        [TestCase(-1, 1f)]
        [TestCase(-1, -1f)]
        public void Run_HoldingStraightUpOrDown_RunsTowardOpponent(int opponentSide, float heldY)
        {
            Vector2 p = SliceArena.OpenFloor;
            using var h = new MatchHarness(p, cpuStart: new Vector2(p.x + opponentSide * 8f, -13.3f));
            h.PlayerInput.HeldMove = new Vector2(0f, heldY);
            h.Tick(2);
            Vector2 p0 = h.Player.Position;
            h.PlayerInput.PressRunOnce(); // up/down still held on the press
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(p0.x + opponentSide * h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(p0.y, h.Player.Position.y, 1e-6f, "a run never moves in depth");
            Assert.AreEqual(opponentSide, h.Player.HorizontalFacing);
        }

        // Replaces Run_FacingDiagonal_RunsHorizontally (D-020). D-037: a diagonal held on the Run press runs
        // along its left/right part (the CPU is to the right, so -x runs away from it), never in depth.
        [TestCase(-1f, 1f, -1)]
        [TestCase(-1f, -1f, -1)]
        [TestCase(1f, 1f, 1)]
        [TestCase(1f, -1f, 1)]
        public void Run_HoldingDiagonal_RunsAlongHeldHorizontal(float x, float y, int expectedDir)
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(x, y);
            h.Tick(2);
            Vector2 p0 = h.Player.Position;
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(p0.x + expectedDir * h.Tuning.runSpeed * 10 * MatchHarness.Dt, h.Player.Position.x, Tol);
            Assert.AreEqual(p0.y, h.Player.Position.y, 1e-6f);
        }

        // D-037 "left/right held" uses the movement deadzone: a left tilt inside it counts as nothing held
        // (toward the opponent, right); just outside it runs left. Deadzone read from tuning.
        [Test]
        public void Run_LeftTiltInsideDeadzone_RunsTowardOpponent_OutsideRunsLeft()
        {
            using (var h = new MatchHarness(SliceArena.OpenFloor))
            {
                float x0 = h.Player.Position.x;
                h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(-h.Tuning.moveDeadzone * 0.5f, 0f),
                    ButtonState.None, new ButtonState(true, true), ButtonState.None));
                h.Tick(5);
                Assert.AreEqual(CombatState.Running, h.Player.State);
                Assert.Greater(h.Player.Position.x, x0, "inside the deadzone: toward the opponent");
            }
            using (var h = new MatchHarness(SliceArena.OpenFloor))
            {
                float x0 = h.Player.Position.x;
                h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(-(h.Tuning.moveDeadzone + 0.1f), 0f),
                    ButtonState.None, new ButtonState(true, true), ButtonState.None));
                h.Tick(5);
                Assert.AreEqual(CombatState.Running, h.Player.State);
                Assert.Less(h.Player.Position.x, x0, "outside the deadzone: the held direction");
            }
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

        // D-020: the run stops at an obstacle. D-027: the truck and pool put the runner in Struck with
        // obstacle collision damage; after Struck the runner stands. (Updated in combat-core step 2.)
        [Test]
        public void Run_StopsAtTruck_StruckThenStanding()
        {
            using var h = new MatchHarness(new Vector2(14f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("running into truck"));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Running, CombatState.Struck)));
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.obstacleCollisionDamage, h.Player.Health, 1e-4f);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            float face = SliceArena.Truck.XMax + h.Tuning.feetHalfWidth;
            Assert.That(h.Player.Position.x, Is.InRange(face, face + 0.01f));
        }

        [Test]
        public void Run_StopsAtPool_StruckThenStanding()
        {
            using var h = new MatchHarness(new Vector2(10f, SliceArena.ObstacleLaneY));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("running into pool"));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Running, CombatState.Struck)));
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.obstacleCollisionDamage, h.Player.Health, 1e-4f);
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
            h.PlayerInput.PressRunOnce(); // D-037: left held on the press -> into the edge
            h.PlayerInput.Release();
            h.Tick(1);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "Standing again: faces the CPU");
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
            h.PlayerInput.PressRunOnce(); // D-037: left held on the press
            h.PlayerInput.Release();
            h.Tick(5);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(-1, h.Player.HorizontalFacing);
        }
    }
}
