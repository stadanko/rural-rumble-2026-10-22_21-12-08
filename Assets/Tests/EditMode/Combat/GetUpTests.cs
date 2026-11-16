using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-043 get-up phase: when the knockdown timer runs out the wrestler stays Knocked Down for the Get-up time
    /// (crouch), with no control and no mashing; only a Stomp can hit (and can KO); then Standing or Recovering.
    /// </summary>
    public class GetUpTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        /// <summary>CPU knocked down by one Strike 1 (32 -> 29, below the threshold) with a fixed knockdown length.</summary>
        private static MatchHarness DownedCpu(float knockdown, float getUp, System.Action<CombatTuning> extra = null)
        {
            var h = new MatchHarness(P, C, configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = knockdown; t.maxKnockdownTime = knockdown;
                t.getUpTime = getUp;
                t.knockdownSlideDistance = 0f; // D-041 slide off: keeps the stomper in range
                extra?.Invoke(t);
            });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            return h;
        }

        [Test]
        public void Default_GetUpTimeIsQuarterSecond()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            Assert.AreEqual(0.25f, t.getUpTime, 1e-6f);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void GetUp_StartsWhenTimerEnds_AndLastsGetUpTime()
        {
            using var h = DownedCpu(1.0f, 0.25f);
            Assert.IsFalse(h.Cpu.IsGettingUp);
            Assert.AreEqual(0.25f, h.Cpu.GetUpDuration, 1e-6f);

            float toGetUp = h.TickUntil(() => h.Cpu.IsGettingUp, 3f, Dt);
            Assert.AreEqual(1.0f, toGetUp, 2 * Dt, "get-up starts when the knockdown timer runs out");
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State, "the get-up is still Knocked Down");
            Assert.AreEqual(0f, h.CpuW.KnockdownTimeRemaining, "KnockdownTimeRemaining is the mashable timer only");
            Assert.Greater(h.Cpu.GetUpTimeRemaining, 0f);
            Assert.LessOrEqual(h.Cpu.GetUpTimeRemaining, 0.25f);

            float up = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 3f, Dt);
            Assert.AreEqual(0.25f, up, 2 * Dt);
            Assert.IsFalse(h.Cpu.IsGettingUp);
            Assert.AreEqual(0f, h.Cpu.GetUpTimeRemaining);
        }

        [Test]
        public void WholeKnockdown_IsTimerPlusGetUp()
        {
            using var h = DownedCpu(1.0f, 0.25f);
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 3f, Dt);
            Assert.AreEqual(1.25f, t, 2 * Dt);
            Assert.AreEqual(1.0f, h.KnockDowns[0].duration, 1e-4f, "KnockedDown event still reports the knockdown timer");
        }

        [TestCase(31f, CombatState.Standing)]   // at or above the Recovering threshold (30)
        [TestCase(29f, CombatState.Recovering)] // below it
        public void AfterGetUp_StandingOrRecoveringByHealth(float healthAfter, CombatState expected)
        {
            // Strike 3 always knocks down; Strike 1/2 deal 0, so health after = maxHealth - strike3Damage.
            using var h = new MatchHarness(P, C, configure: t =>
            {
                t.strike1Damage = 0f; t.strike2Damage = 0f; t.strike3Damage = 1f; t.maxHealth = healthAfter + 1f;
                t.getUpTime = 0.25f;
            });
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            h.TickUntil(() => h.Cpu.IsGettingUp, 5f, Dt);
            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 1f, Dt);
            Assert.AreEqual(expected, h.Cpu.State);
        }

        [Test]
        public void GetUp_NoControl()
        {
            using var h = DownedCpu(0.5f, 1.0f);
            h.TickUntil(() => h.Cpu.IsGettingUp, 2f, Dt);
            Vector2 pos = h.Cpu.Position;
            Vector2 facing = h.Cpu.Facing;
            int moves = h.MovesStarted.Count;
            int states = h.StateChanges.Count;
            // Strike, Lock Up, Run and a held direction on alternate ticks.
            h.CpuInput.HeldMove = Vector2.right + Vector2.up;
            for (int i = 0; i < 20; i++)
            {
                h.CpuInput.PressStrikeOnce();
                h.CpuInput.PressGrappleOnce();
                h.CpuInput.PressRunOnce();
            }
            h.Tick(60, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.IsTrue(h.Cpu.IsGettingUp);
            V2Assert.AreEqual(pos, h.Cpu.Position, 1e-6f, "no movement during the get-up");
            Assert.AreEqual(facing, h.Cpu.Facing, "facing stays locked");
            Assert.AreEqual(moves, h.MovesStarted.Count, "no move starts");
            Assert.AreEqual(states, h.StateChanges.Count, "no state change");
        }

        [Test]
        public void GetUp_MashingDoesNotShortenIt()
        {
            using var h = DownedCpu(0.5f, 0.25f);
            h.TickUntil(() => h.Cpu.IsGettingUp, 2f, Dt);
            for (int i = 0; i < 100; i++) h.CpuInput.PressStrikeOnce();
            float up = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 1f, Dt);
            Assert.AreEqual(0.25f, up, 2 * Dt);
        }

        [Test]
        public void Mashing_ShortensTimer_ButGetUpStillAdded()
        {
            using var h = DownedCpu(2.0f, 0.25f, t => t.mashReductionPerPress = 1f);
            for (int i = 0; i < 400; i++) h.CpuInput.PressStrikeOnce();
            float toGetUp = h.TickUntil(() => h.Cpu.IsGettingUp, 3f, Dt);
            float up = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 1f, Dt);
            Assert.AreEqual(h.Tuning.shortestKnockdown, toGetUp, 2 * Dt);
            Assert.AreEqual(0.25f, up, 2 * Dt);
        }

        [Test]
        public void GetUp_StompLands_DamageOnly()
        {
            using var h = DownedCpu(0.3f, 3.0f);
            h.TickUntil(() => h.Cpu.IsGettingUp, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            Assert.IsTrue(h.Cpu.IsGettingUp);
            float health = h.Cpu.Health;
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Player.CurrentMove, "Strike over a getting-up opponent is a Stomp");
            h.TickUntil(() => h.Hits.Count >= 2, 1f, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Hits[1].Move);
            Assert.AreEqual(health - h.Tuning.stompDamage, h.Cpu.Health, 1e-4f);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.IsTrue(h.Cpu.IsGettingUp, "a stomp doesn't change the get-up");
            Assert.AreEqual(1, h.KnockDowns.Count, "no new knockdown");
        }

        [Test]
        public void GetUp_StompCanKnockOut()
        {
            using var h = DownedCpu(0.3f, 3.0f, t => t.stompDamage = 50f);
            h.TickUntil(() => h.Cpu.IsGettingUp, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            Assert.IsTrue(h.Cpu.IsGettingUp);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(WrestlerId.Player, h.Match.Result.Winner);
            Assert.AreEqual(MoveId.Stomp, h.Hits.Last().Move);
            Assert.IsFalse(h.Cpu.IsGettingUp);
            Assert.AreEqual(0f, h.Cpu.GetUpTimeRemaining);
        }

        [Test]
        public void GetUp_ComboStrikeDoesNotLand()
        {
            // 2.6 apart: outside stomp range, inside punch reach.
            using var h = new MatchHarness(P, P + new Vector2(2.6f, 0f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 0.3f; t.maxKnockdownTime = 0.3f; t.getUpTime = 3f;
                t.knockdownSlideDistance = 0f;
            });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.IsGettingUp, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(MoveId.Strike1, h.MovesStarted.Last().move);
            Assert.AreEqual(1, h.Hits.Count, "only a Stomp can hit during the get-up");
        }

        [Test]
        public void GetUp_NoExtraEvents()
        {
            using var h = DownedCpu(0.5f, 0.25f);
            int states = h.StateChanges.Count;
            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 2f, Dt);
            var cpuChanges = h.StateChanges.Skip(states).Where(s => s.id == WrestlerId.Cpu).ToList();
            Assert.AreEqual(1, cpuChanges.Count, "only KnockedDown -> Recovering");
            Assert.AreEqual(CombatState.KnockedDown, cpuChanges[0].from);
            Assert.AreEqual(CombatState.Recovering, cpuChanges[0].to);
            Assert.AreEqual(1, h.KnockDowns.Count);
        }

        [Test]
        public void GetUpTimeZero_GetsUpWhenTimerEnds()
        {
            using var h = DownedCpu(1.0f, 0f);
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 3f, Dt);
            Assert.AreEqual(1.0f, t, 2 * Dt);
        }

        [Test]
        public void TuningEdit_GetUpTime_AppliesToNextGetUp()
        {
            using var h = DownedCpu(0.5f, 0.25f);
            h.Tuning.getUpTime = 0.6f;
            Assert.AreEqual(0.6f, h.Cpu.GetUpDuration, 1e-6f);
            h.TickUntil(() => h.Cpu.IsGettingUp, 2f, Dt);
            float up = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 2f, Dt);
            Assert.AreEqual(0.6f, up, 2 * Dt);
        }

        [Test]
        public void Bodyslam_Receiver_GetsUpTheSameWay()
        {
            using var h = new MatchHarness(P, new Vector2(11.5f, -12f), configure: t =>
            {
                t.minKnockdownTime = t.maxKnockdownTime = t.shortestKnockdown = 0.5f;
                t.getUpTime = 0.25f;
            });
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 4f, Dt);
            Assert.AreEqual(0, h.Cpu.FallSide, "bodyslam knockdown (no fall)");
            float toGetUp = h.TickUntil(() => h.Cpu.IsGettingUp, 2f, Dt);
            Assert.AreEqual(0.5f, toGetUp, 2 * Dt);
            float up = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 2f, Dt);
            Assert.AreEqual(0.25f, up, 2 * Dt);
        }
    }
}
