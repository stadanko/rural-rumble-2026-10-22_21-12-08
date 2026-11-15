using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-041 knockdown fall: <see cref="IWrestlerView.FallSide"/> per trigger (strike, obstacle, run collision, knockout
    /// from standing; none for the bodyslam or a stomp KO while down), and the knockback slide (knockdownSlideDistance
    /// over knockdownSlideDuration on x, away from what hit them, clamped like any movement, knockdown length unchanged).
    /// Knockdowns are made long (10 s) where the slide is measured, so the wrestler is still down.
    /// </summary>
    public class KnockdownFallTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        private static void LongKnockdown(CombatTuning t)
        {
            t.minKnockdownTime = 10f;
            t.maxKnockdownTime = 10f;
        }

        // ================================================================ FallSide per trigger

        [TestCase(10f, 12f, 1)]  // attacker left of the defender: falls toward +x
        [TestCase(12f, 10f, -1)] // attacker right of the defender: falls toward -x
        public void Strike3Knockdown_FallsAwayFromAttacker(float playerX, float cpuX, int expected)
        {
            using var h = new MatchHarness(new Vector2(playerX, -12f), new Vector2(cpuX, -12f));
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(MoveId.Strike3, h.Hits.Last().Move);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(expected, h.Cpu.FallSide);
            Assert.AreEqual(0, h.Player.FallSide, "the attacker is not falling");
        }

        [Test]
        public void ThresholdKnockdown_FallsAwayFromAttacker()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Assert.AreEqual(MoveId.Strike1, h.Hits.Last().Move);
            Assert.AreEqual(1, h.Cpu.FallSide);
        }

        // Exactly stacked on x (different depth): the opposite of HorizontalFacing.
        [Test]
        public void StrikeKnockdown_TieOnX_FallsOppositeHorizontalFacing()
        {
            using var h = new MatchHarness(new Vector2(12f, -12f), new Vector2(12f, -12.3f), configure: t => t.maxHealth = 32f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Assert.AreEqual(h.Player.Position.x, h.Cpu.Position.x, 1e-6f, "same x when hit (the slide starts next tick)");
            Assert.AreNotEqual(0, h.Cpu.HorizontalFacing);
            Assert.AreEqual(-h.Cpu.HorizontalFacing, h.Cpu.FallSide);
        }

        // Running left into the truck: away from the obstacle = +x.
        [Test]
        public void ObstacleKnockdown_FallsOppositeRunDirection()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f),
                configure: t => { t.maxHealth = 37f; LongKnockdown(t); });
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(1, h.Player.FallSide);

            float x = h.Player.Position.x;
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(x + h.Tuning.knockdownSlideDistance, h.Player.Position.x, 1e-4f, "slides away from the truck");
        }

        // Running right into the pool: away from the obstacle = -x.
        [Test]
        public void ObstacleKnockdown_RunningRight_FallsTowardMinusX()
        {
            using var h = new MatchHarness(new Vector2(10f, SliceArena.ObstacleLaneY), configure: t => t.maxHealth = 37f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(-1, h.Player.FallSide);
        }

        [Test]
        public void RunCollision_BothFallAwayFromEachOther_AndSlideApart()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f), configure: LongKnockdown);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(-1, h.Player.FallSide, "the runner came from the left: falls back toward -x");
            Assert.AreEqual(1, h.Cpu.FallSide);

            float gap = h.Cpu.Position.x - h.Player.Position.x;
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(gap + 2f * h.Tuning.knockdownSlideDistance, h.Cpu.Position.x - h.Player.Position.x, 1e-4f);
        }

        // ================================================================ Knockout from standing: fall side and slide after IsOver

        /// <summary>Everything a frozen match must not change while the knocked-out wrestler slides.</summary>
        private static string Frozen(MatchHarness h) =>
            $"t={h.Match.ElapsedSeconds:R} states={h.StateChanges.Count} facing={h.FacingChanges.Count} hits={h.Hits.Count} " +
            $"health={h.HealthChanges.Count} kd={h.KnockDowns.Count} ends={h.MatchEnds.Count} moves={h.MovesStarted.Count} " +
            $"phases={h.PhaseChanges.Count} obstacles={h.ObstacleHits.Count} paired={h.PairedMoves.Count} " +
            $"P={h.Player.State}/{h.Player.Health:R}/{h.Player.HorizontalFacing}/{h.Player.FallSide} " +
            $"C={h.Cpu.State}/{h.Cpu.Health:R}/{h.Cpu.HorizontalFacing}/{h.Cpu.FallSide}";

        [Test]
        public void KnockoutFromStanding_Strike_FallsAndSlidesAway_WinnerAndMatchFrozen()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 3f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(1, h.Cpu.FallSide);
            Assert.AreEqual(1, h.MatchEnds.Count);

            Vector2 ko = h.Cpu.Position, winner = h.Player.Position;
            string frozen = Frozen(h);
            h.PlayerInput.HeldMove = Vector2.left; // the winner's input does nothing any more
            h.CpuInput.HeldMove = Vector2.right;
            h.Tick(10, Dt); // 0.10 s of 0.35 s: linear like a knockdown slide
            Assert.AreEqual(ko.x + h.Tuning.knockdownSlideDistance * (0.10f / h.Tuning.knockdownSlideDuration), h.Cpu.Position.x, 1e-4f);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(ko.x + h.Tuning.knockdownSlideDistance, h.Cpu.Position.x, 1e-4f, "KO slide away from the attacker");
            Assert.AreEqual(ko.y, h.Cpu.Position.y, 1e-6f);
            V2Assert.AreEqual(winner, h.Player.Position, 1e-6f, "the winner is frozen");
            Assert.AreEqual(frozen, Frozen(h), "no timer, state, health or event changes during the KO slide");
            Assert.AreEqual(1, h.MatchEnds.Count, "MatchEnded once");
        }

        [Test]
        public void KnockoutFromStanding_Obstacle_FallsAndSlidesAwayFromObstacle()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f), configure: t => t.maxHealth = 8f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Player.State);
            Assert.AreEqual(1, h.Player.FallSide);

            Vector2 ko = h.Player.Position, winner = h.Cpu.Position;
            string frozen = Frozen(h);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(ko.x + h.Tuning.knockdownSlideDistance, h.Player.Position.x, 1e-4f, "slides away from the truck");
            V2Assert.AreEqual(winner, h.Cpu.Position, 1e-6f, "the winner is frozen");
            Assert.AreEqual(frozen, Frozen(h), "no ObstacleHit or other event during the KO slide");
        }

        [Test]
        public void KnockoutFromStanding_RunCollision_BothFallAndSlideApart()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f), configure: t => t.runCollisionDamage = 1000f);
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.IsTrue(h.Match.Result.IsDraw);
            Assert.AreEqual(-1, h.Player.FallSide);
            Assert.AreEqual(1, h.Cpu.FallSide);

            Vector2 p = h.Player.Position, c = h.Cpu.Position;
            string frozen = Frozen(h);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(p.x - h.Tuning.knockdownSlideDistance, h.Player.Position.x, 1e-4f);
            Assert.AreEqual(c.x + h.Tuning.knockdownSlideDistance, h.Cpu.Position.x, 1e-4f);
            Assert.AreEqual(frozen, Frozen(h));
            Assert.AreEqual(1, h.MatchEnds.Count, "MatchEnded once");
        }

        [Test]
        public void KnockoutSlide_StopsAtArenaEnd()
        {
            float maxX = SliceArena.BandXMax - 0.5f; // feetHalfWidth 0.5
            using var h = new MatchHarness(new Vector2(maxX - 2f, -12f), new Vector2(maxX - 0.2f, -12f), configure: t => t.maxHealth = 3f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(maxX, h.Cpu.Position.x, 0.01f, "clamped at the right arena end (arena skin)");
        }

        [Test]
        public void BodyslamKnockdown_FallSideIsZero_AndNoSlide()
        {
            using var h = new MatchHarness(P, new Vector2(11.5f, -12f), configure: LongKnockdown);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(0, h.Cpu.FallSide);
            Vector2 at = h.Cpu.Position;
            h.TickSeconds(0.5f, Dt);
            V2Assert.AreEqual(at, h.Cpu.Position, 1e-6f, "a bodyslam knockdown does not slide");
            Assert.AreEqual(0, h.Cpu.FallSide);
        }

        [Test]
        public void BodyslamKnockout_FallSideIsZero()
        {
            using var h = new MatchHarness(P, new Vector2(11.5f, -12f), configure: t => t.bodyslamDamage = 1000f);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.TickUntil(() => h.Match.IsOver, 5f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(0, h.Cpu.FallSide);
        }

        [Test]
        public void StompKnockoutWhileDown_FallSideIsZero()
        {
            // Knockdown slide off so the stomper stays in stomp range without stepping in; switched back on for the KO.
            using var h = new MatchHarness(P, C, configure: t =>
            {
                t.maxHealth = 32f; LongKnockdown(t); t.stompDamage = 50f; t.knockdownSlideDistance = 0f;
            });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            Assert.AreEqual(1, h.Cpu.FallSide, "the knockdown itself falls");
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.TickSeconds(h.Tuning.knockdownSlideDuration, Dt); // the (zero-distance) knockdown slide is over
            h.Tuning.knockdownSlideDistance = 0.5f;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Hits.Last().Move);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(0, h.Cpu.FallSide, "a KO while already down doesn't fall again");
            Vector2 at = h.Cpu.Position;
            h.TickSeconds(1f, Dt);
            V2Assert.AreEqual(at, h.Cpu.Position, 1e-6f, "a stomp KO while down does not slide");
        }

        [Test]
        public void FallSide_ResetsToZero_WhenTheKnockdownEnds()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Assert.AreEqual(1, h.Cpu.FallSide);
            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(0, h.Cpu.FallSide);
        }

        // ================================================================ Slide

        [Test]
        public void Slide_MovesDistanceAwayOnX_LinearOverDuration_DepthUnchanged()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; LongKnockdown(t); });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Vector2 start = h.Cpu.Position;
            float d = h.Tuning.knockdownSlideDistance, T = h.Tuning.knockdownSlideDuration;
            Assert.AreEqual(0.5f, d, 1e-6f, "default (D-041)");
            Assert.AreEqual(0.35f, T, 1e-6f, "default (D-041)");

            h.Tick(10, Dt); // 0.10 s of 0.35 s
            Assert.AreEqual(start.x + d * (0.10f / T), h.Cpu.Position.x, 1e-4f, "linear");

            h.TickSeconds(T, Dt);
            Assert.AreEqual(start.x + d, h.Cpu.Position.x, 1e-4f);
            Assert.AreEqual(start.y, h.Cpu.Position.y, 1e-6f);

            h.TickSeconds(1f, Dt);
            Assert.AreEqual(start.x + d, h.Cpu.Position.x, 1e-4f, "no further movement after the slide");
        }

        [Test]
        public void Slide_TowardMinusX_WhenHitFromTheRight()
        {
            using var h = new MatchHarness(new Vector2(12f, -12f), new Vector2(10f, -12f), configure: t => { t.maxHealth = 32f; LongKnockdown(t); });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float x = h.Cpu.Position.x;
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(x - h.Tuning.knockdownSlideDistance, h.Cpu.Position.x, 1e-4f);
        }

        // D-005: read at use time.
        [Test]
        public void Slide_TuningEdit_AppliesToTheNextKnockdown()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; LongKnockdown(t); });
            h.Tuning.knockdownSlideDistance = 1.2f;
            h.Tuning.knockdownSlideDuration = 0.1f;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float x = h.Cpu.Position.x;
            h.TickSeconds(0.15f, Dt);
            Assert.AreEqual(x + 1.2f, h.Cpu.Position.x, 1e-4f);
        }

        [Test]
        public void Slide_StopsAtArenaEnd()
        {
            float maxX = SliceArena.BandXMax - 0.5f; // feetHalfWidth 0.5
            using var h = new MatchHarness(new Vector2(maxX - 2f, -12f), new Vector2(maxX - 0.2f, -12f),
                configure: t => { t.maxHealth = 32f; LongKnockdown(t); });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Assert.AreEqual(1, h.Cpu.FallSide);
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(maxX, h.Cpu.Position.x, 0.01f, "clamped at the right arena end (arena skin)");
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
        }

        [Test]
        public void Slide_StopsAtObstacle()
        {
            float stopX = SliceArena.Pool.XMin - 0.5f; // feet box against the pool's left side
            using var h = new MatchHarness(new Vector2(stopX - 2.2f, SliceArena.ObstacleLaneY), new Vector2(stopX - 0.2f, SliceArena.ObstacleLaneY),
                configure: t => { t.maxHealth = 32f; LongKnockdown(t); });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            Assert.AreEqual(1, h.Cpu.FallSide);
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(stopX, h.Cpu.Position.x, 0.01f, "clamped at the pool (arena skin)");
            Assert.IsEmpty(h.ObstacleHits, "a slide into an obstacle is not an obstacle hit");
            foreach (ArenaRect o in h.Match.Arena.Obstacles)
                Assert.IsFalse(SliceArena.FeetInsideObstacle(h.Cpu.Position, h.HalfExtents, o));
        }

        // The slide must not change knockdown length: same duration and same get-up time with and without it.
        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(3f)]
        public void Slide_DoesNotChangeKnockdownDuration(float slideDistance)
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.knockdownSlideDistance = slideDistance; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float expected = Mathf.Lerp(h.Tuning.minKnockdownTime, h.Tuning.maxKnockdownTime,
                (h.Tuning.maxHealth - h.Cpu.Health) / h.Tuning.maxHealth);
            Assert.AreEqual(expected, h.KnockDowns[0].duration, 1e-4f);
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(h.KnockDowns[0].duration, t, 2 * Dt);
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.strike1Damage, h.Cpu.Health, 1e-4f, "no extra damage");
        }
    }
}
