using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-027 / D-029 running rules: Run cancels your own run, Strike ends it with Strike 1, Lock Up does
    /// nothing, truck/pool hits, running into the opponent knocks both down.
    /// </summary>
    public class RunRulesTests
    {
        private const float Dt = 0.01f;

        [Test]
        public void RunAgain_CancelsOwnRun_StopsOnTheSpot()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(25f, -13f));
            h.PlayerInput.PressRunOnce();
            h.Tick(10, Dt);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            h.PlayerInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Vector2 p = h.Player.Position;
            h.Tick(20, Dt);
            V2Assert.AreEqual(p, h.Player.Position, 1e-6f);
        }

        [Test]
        public void StrikeWhileRunning_EndsRun_ThrowsStrike1OnTheSpot()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(25f, -13f));
            h.PlayerInput.PressRunOnce();
            h.Tick(10, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Running, CombatState.Striking)));
            Vector2 p = h.Player.Position;
            h.TickSeconds(0.6f, Dt);
            V2Assert.AreEqual(p, h.Player.Position, 1e-6f);
            Assert.AreEqual(CombatState.Standing, h.Player.State, "the run does not resume after the strike");
        }

        [Test]
        public void LockUpWhileRunning_DoesNothing()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(25f, -13f));
            h.PlayerInput.PressRunOnce();
            h.Tick(10, Dt);
            float x = h.Player.Position.x;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(x + h.Tuning.runSpeed * Dt, h.Player.Position.x, 1e-4f);
        }

        [Test]
        public void RunIntoTruck_ObstacleHitEvent_StruckAndDamage()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f));
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(WrestlerId.Player, h.ObstacleHits[0].id);
            Assert.AreEqual(SliceArena.Truck.XMax, h.ObstacleHits[0].contact.x, 0.01f);
            Assert.AreEqual(CombatState.Struck, h.Player.State);
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.obstacleCollisionDamage, h.Player.Health, 1e-4f);
            Assert.IsEmpty(h.Hits, "an obstacle is not a HitLanded");
        }

        [Test]
        public void RunIntoArenaEndOrBackWall_NoDamageNoEvent()
        {
            using var h = new MatchHarness(new Vector2(20f, -13.5f), new Vector2(26f, -11f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(h.Tuning.maxHealth, h.Player.Health);
            Assert.IsEmpty(h.ObstacleHits);
        }

        // D-029: running into the opponent knocks both down, each with their own health-based knockdown.
        [Test]
        public void RunIntoOpponent_KnocksBothDown()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f));
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            CollectionAssert.AreEquivalent(new[] { WrestlerId.Player, WrestlerId.Cpu }, h.KnockDowns.Select(k => k.id).ToArray());
            Assert.AreEqual(h.Tuning.minKnockdownTime, h.KnockDowns[0].duration, 1e-4f, "full health -> minimum knockdown");
            Assert.AreEqual(h.Tuning.maxHealth, h.Player.Health, "run-collision damage defaults to 0");
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health);
            Assert.That(h.Cpu.Position.x - h.Player.Position.x, Is.GreaterThanOrEqualTo(2f * h.Tuning.feetHalfWidth - 1e-4f));
        }

        [Test]
        public void RunIntoOpponent_RunCollisionDamage_IsTunable_AndSetsKnockdownLength()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f), configure: t => t.runCollisionDamage = 10f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(h.Tuning.maxHealth - 10f, h.Player.Health, 1e-4f);
            Assert.AreEqual(h.Tuning.maxHealth - 10f, h.Cpu.Health, 1e-4f);
            float expected = Mathf.Lerp(h.Tuning.minKnockdownTime, h.Tuning.maxKnockdownTime, 0.1f);
            Assert.IsTrue(h.KnockDowns.All(k => Mathf.Abs(k.duration - expected) < 1e-4f));
        }

        [Test]
        public void BothRunHeadOn_BothKnockedDownOnce()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(15f, -12f));
            h.CpuIdle = false;
            h.PlayerInput.PressRunOnce();
            h.CpuInput.PressRunOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(2, h.KnockDowns.Count);
            Assert.AreEqual(20f, h.Player.Position.x + h.Cpu.Position.x, 0.01f, "met in the middle (symmetric about x = 10)");
        }

        /// <summary>CPU knocked down for 10 s by a Strike 1 (32 -> 29); player then backs off to x = 7 facing right.</summary>
        private static MatchHarness DownedCpuPlayerBackedOff()
        {
            var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f),
                configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 10f; t.maxKnockdownTime = 10f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(1f, Dt);
            h.PlayerInput.Release();
            h.PlayerInput.Enqueue(new WrestlerIntent(Vector2.right, ButtonState.None, ButtonState.None, ButtonState.None));
            h.Tick(1, Dt);
            return h;
        }

        // D-030: downed wrestlers don't block runs; the runner passes over and keeps running.
        [Test]
        public void RunIntoKnockedDownOpponent_PassesOver_NoCollision()
        {
            using var h = DownedCpuPlayerBackedOff();
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Player.Position.x > h.Cpu.Position.x + 1.5f, 2f, Dt);
            Assert.AreEqual(CombatState.Running, h.Player.State, "the run continues past the downed opponent");
            Assert.AreEqual(1, h.KnockDowns.Count, "no run collision with a downed opponent");
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            V2Assert.AreEqual(new Vector2(12f, -12f), h.Cpu.Position, 1e-6f, "the downed wrestler is not moved");
        }

        // Walking (not running) into a downed wrestler still blocks (Combat's choice; D-030 covers runs only).
        [Test]
        public void WalkIntoKnockedDownOpponent_StillBlocked()
        {
            using var h = DownedCpuPlayerBackedOff();
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(2f, Dt);
            Assert.That(h.Cpu.Position.x - h.Player.Position.x, Is.InRange(2f * h.Tuning.feetHalfWidth - 1e-4f, 2f * h.Tuning.feetHalfWidth + 0.01f));
        }

        // After passing over and stopping on top of a downed wrestler, both can still move apart.
        [Test]
        public void StopOnTopOfDownedOpponent_CanWalkOff()
        {
            using var h = DownedCpuPlayerBackedOff();
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Player.Position.x > h.Cpu.Position.x - 0.2f, 2f, Dt);
            h.PlayerInput.PressRunOnce(); // cancel on top of them
            h.Tick(1, Dt);
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(1f, Dt);
            Assert.Greater(h.Player.Position.x, h.Cpu.Position.x + 1f);
        }

        // D-030: a crash into the truck or pool that leaves health below 30 knocks down instead of Struck.
        [Test]
        public void RunIntoTruck_BelowThresholdAfterDamage_KnockedDown()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f), configure: t => t.maxHealth = 37f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(29f, h.Player.Health, 1e-4f);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(1, h.KnockDowns.Count);
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Running, CombatState.KnockedDown)));
        }

        [Test]
        public void RunIntoPool_AtOrAboveThresholdAfterDamage_Struck()
        {
            using var h = new MatchHarness(new Vector2(10f, SliceArena.ObstacleLaneY), configure: t => t.maxHealth = 38f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(30f, h.Player.Health, 1e-4f);
            Assert.AreEqual(CombatState.Struck, h.Player.State);
            Assert.IsEmpty(h.KnockDowns);
        }

        [Test]
        public void RunIntoTruck_ToZero_KnockedOut()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f), configure: t => t.maxHealth = 8f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Player.State);
            Assert.AreEqual(WrestlerId.Cpu, h.Match.Result.Winner);
            Assert.IsFalse(h.Match.Result.IsDraw);
        }

        [Test]
        public void RunPastOpponentOnAnotherDepth_NoCollision()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12.6f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(1.5f, Dt);
            Assert.IsEmpty(h.KnockDowns);
            Assert.Greater(h.Player.Position.x, 13f);
        }
    }

    /// <summary>
    /// D-027 wrestler blocking: feet can't overlap, no pushing, and resolution doesn't favor either wrestler.
    /// </summary>
    public class BlockingTests
    {
        private const float Dt = 0.02f;

        [Test]
        public void WalkIntoIdleOpponent_StopsAtContact_NoPush()
        {
            var cpuStart = new Vector2(12f, -12f);
            using var h = new MatchHarness(new Vector2(8f, -12f), cpuStart);
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(3f, Dt);
            Assert.AreEqual(cpuStart.x - 2f * h.Tuning.feetHalfWidth, h.Player.Position.x, 0.01f);
            Assert.That(cpuStart.x - h.Player.Position.x, Is.GreaterThanOrEqualTo(2f * h.Tuning.feetHalfWidth));
            V2Assert.AreEqual(cpuStart, h.Cpu.Position, 1e-6f, "no pushing");
            Assert.AreEqual(CombatState.Walking, h.Player.State);
        }

        [Test]
        public void BothWalkToward_MeetSymmetrically()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(12.03f, -12f));
            h.CpuIdle = false;
            h.PlayerInput.HeldMove = Vector2.right;
            h.CpuInput.HeldMove = Vector2.left;
            h.TickSeconds(2f, Dt);
            float playerTravel = h.Player.Position.x - 8f;
            float cpuTravel = 12.03f - h.Cpu.Position.x;
            Assert.AreEqual(playerTravel, cpuTravel, 1e-4f, "neither wrestler favored");
            Assert.That(h.Cpu.Position.x - h.Player.Position.x, Is.GreaterThanOrEqualTo(2f * h.Tuning.feetHalfWidth - 1e-5f));
        }

        // Swapping which side is the player gives mirrored results (processing order doesn't matter).
        [Test]
        public void RolesSwapped_SameOutcome()
        {
            Vector2 a = new Vector2(8f, -12f), b = new Vector2(12.05f, -12.1f);
            using var h1 = new MatchHarness(a, b);
            using var h2 = new MatchHarness(b, a);
            h1.CpuIdle = h2.CpuIdle = false;
            h1.PlayerInput.HeldMove = new Vector2(1f, -0.2f); h1.CpuInput.HeldMove = new Vector2(-1f, 0.2f);
            h2.CpuInput.HeldMove = new Vector2(1f, -0.2f); h2.PlayerInput.HeldMove = new Vector2(-1f, 0.2f);
            h1.TickSeconds(2f, Dt);
            h2.TickSeconds(2f, Dt);
            V2Assert.AreEqual(h1.Player.Position, h2.Cpu.Position, 1e-5f);
            V2Assert.AreEqual(h1.Cpu.Position, h2.Player.Position, 1e-5f);
        }

        [Test]
        public void DifferentDepth_WalkPast()
        {
            float gap = 2f * 0.25f + 0.05f;
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(12f, -12f - gap));
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(3f, Dt);
            Assert.Greater(h.Player.Position.x, 13f);
        }

        [Test]
        public void WalkInDepthIntoOpponent_StopsAtContact()
        {
            using var h = new MatchHarness(new Vector2(12f, -13f), new Vector2(12.3f, -11.5f));
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(2f, Dt);
            Assert.AreEqual(-11.5f - 2f * h.Tuning.wrestlerBlockHalfDepth, h.Player.Position.y, 0.01f);
        }

        // Feet never overlap during a long scramble of both wrestlers walking and running at each other.
        [Test]
        public void Scramble_FeetNeverOverlap()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12.2f), configure: t => t.runCollisionDamage = 0f);
            h.CpuIdle = false;
            var half = new Vector2(h.Tuning.feetHalfWidth, h.Tuning.wrestlerBlockHalfDepth);
            Vector2[] moves = { Vector2.right, new Vector2(1f, 1f), Vector2.up, new Vector2(-1f, -1f), Vector2.down, new Vector2(1f, -1f) };
            for (int i = 0; i < 300; i++)
            {
                h.PlayerInput.HeldMove = moves[i % moves.Length];
                h.CpuInput.HeldMove = -moves[(i / 7) % moves.Length];
                h.Tick(1, Dt);
                Assert.IsFalse(WrestlerBlocking.Overlaps(h.Player.Position, h.Cpu.Position, half),
                    $"tick {i}: {h.Player.Position} overlaps {h.Cpu.Position}");
            }
        }
    }
}
