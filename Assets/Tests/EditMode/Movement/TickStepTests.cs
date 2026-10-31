using NUnit.Framework;
using RuralRumble.Combat;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 4.2 Tick / D-021: CombatMatch.Tick clamps dt to a maximum step (tuning maxTickStep).
    /// ArenaGeometry sweeps against obstacles, so even a large step cannot tunnel through them.
    /// </summary>
    public class TickStepTests
    {
        [Test]
        public void Tick_HugeDt_IsClampedToMaxStep()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(1, dt: 10f);
            Assert.AreEqual(SliceArena.OpenFloor.x + h.Tuning.walkSpeedHorizontal * h.Tuning.maxTickStep, h.Player.Position.x, 1e-4f);
            Assert.AreEqual(h.Tuning.maxTickStep, h.Match.ElapsedSeconds, 1e-6f);
        }

        [Test]
        public void Tick_NegativeDt_DoesNothing()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(1, dt: -1f);
            V2Assert.AreEqual(SliceArena.OpenFloor, h.Player.Position, 1e-6f);
            Assert.AreEqual(0f, h.Match.ElapsedSeconds);
        }

        // Even with the clamp raised (a designer could tune it), a giant step must not pass through the truck.
        [Test]
        public void Run_GiantStepIntoTruck_DoesNotTunnel()
        {
            using var h = new MatchHarness(new Vector2(16f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f),
                configure: t => t.maxTickStep = 100f);
            h.PlayerInput.PressRunOnce();
            h.Tick(1, dt: 100f); // 700 units of travel in one tick
            h.AssertPlayerOutsideObstacles("giant step left");
            Assert.That(h.Player.Position.x, Is.GreaterThanOrEqualTo(SliceArena.Truck.XMax + h.Tuning.feetHalfWidth));
        }

        [Test]
        public void Run_GiantStepIntoPool_DoesNotTunnel()
        {
            using var h = new MatchHarness(new Vector2(10f, SliceArena.ObstacleLaneY), configure: t => t.maxTickStep = 100f);
            h.PlayerInput.PressRunOnce();
            h.Tick(1, dt: 100f);
            h.AssertPlayerOutsideObstacles("giant step right");
            Assert.That(h.Player.Position.x, Is.LessThanOrEqualTo(SliceArena.Pool.XMin - h.Tuning.feetHalfWidth));
        }

        [Test]
        public void Walk_GiantStepUpIntoTruck_DoesNotTunnel()
        {
            using var h = new MatchHarness(new Vector2(4f, -13.5f), configure: t => t.maxTickStep = 100f);
            h.PlayerInput.HeldMove = Vector2.up;
            h.Tick(1, dt: 100f);
            h.AssertPlayerOutsideObstacles("giant step up");
            Assert.That(h.Player.Position.y, Is.LessThanOrEqualTo(SliceArena.Truck.YMin));
        }

        [Test]
        public void Geometry_GiantDelta_StopsAtFirstObstacle()
        {
            ArenaGeometry arena = SliceArena.Build();
            ArenaMoveResult r = arena.Move(new Vector2(25f, SliceArena.ObstacleLaneY), new Vector2(-1000f, 0f), new Vector2(0.5f, 0f));
            // From x = 25 the pool footprint (18.33-27.67) already overlaps, so the box is allowed out of it,
            // but it must still stop at the truck.
            Assert.That(r.Position.x, Is.GreaterThanOrEqualTo(SliceArena.Truck.XMax + 0.5f));
            Assert.IsTrue(r.HitObstacleX);
        }
    }
}
