using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// COMBAT_SPEC Damage, Knocked Down and Tuning Values; D-025 (linear knockdown, mashing, no get-up
    /// invulnerability), D-029 (threshold checked after damage), ARCHITECTURE 4.2 (Knocked Out freezes the match).
    /// Health below full is set up by lowering maxHealth: wrestlers start at maxHealth.
    /// </summary>
    public class KnockdownTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        private static float ExpectedKnockdown(CombatTuning t, float healthAfter) =>
            Mathf.Lerp(t.minKnockdownTime, t.maxKnockdownTime, (t.maxHealth - healthAfter) / t.maxHealth);

        // D-029: 32 -> 29 knocks down (check after damage); 34 -> 31 does not.
        [TestCase(32f, true)]
        [TestCase(34f, false)]
        public void Strike_KnockdownCheckIsAfterDamage(float startHealth, bool knocksDown)
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = startHealth);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(startHealth - h.Tuning.strike1Damage, h.Cpu.Health, 1e-4f);
            Assert.AreEqual(knocksDown ? CombatState.KnockedDown : CombatState.Struck, h.Cpu.State);
        }

        // D-025: length is linear in health lost, from min at full health to max near 0.
        [Test]
        public void KnockdownDuration_LinearInHealthLost()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(WrestlerId.Cpu, h.KnockDowns[0].id);
            Assert.AreEqual(ExpectedKnockdown(h.Tuning, h.Cpu.Health), h.KnockDowns[0].duration, 1e-4f);
            Assert.AreEqual(h.KnockDowns[0].duration, h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        // Strike 3 always knocks down; with Strike 1/2 at 0 damage its own damage sets the health lost.
        [TestCase(0f, 1.0f)]   // full health -> minimum knockdown time
        [TestCase(97f, 2.94f)] // 3 health left -> close to the maximum
        public void KnockdownDuration_Endpoints(float strike3Damage, float expected)
        {
            using var h = new MatchHarness(P, C, configure: t =>
            {
                t.strike1Damage = 0f; t.strike2Damage = 0f; t.strike3Damage = strike3Damage;
            });
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(expected, h.KnockDowns[0].duration, 1e-3f);
        }

        [Test]
        public void Knockdown_EndsStanding_AtOrAboveThreshold()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(h.KnockDowns[0].duration, t, 2 * Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }

        [Test]
        public void Knockdown_EndsRecovering_BelowThreshold()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Cpu.State);
        }

        // COMBAT_SPEC: each Strike or Grapple press removes a share of the remaining time.
        [Test]
        public void Mash_StrikePress_RemovesShareOfRemaining()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            h.Tick(5, Dt);
            float before = h.CpuW.KnockdownTimeRemaining;
            h.CpuInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual((before - Dt) * (1f - h.Tuning.mashReductionPerPress), h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        [Test]
        public void Mash_GrapplePress_AlsoCounts()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float before = h.CpuW.KnockdownTimeRemaining;
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual((before - Dt) * (1f - h.Tuning.mashReductionPerPress), h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        [Test]
        public void Mash_HeldButtonWithoutNewPress_DoesNothing()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 32f);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float before = h.CpuW.KnockdownTimeRemaining;
            h.CpuInput.Enqueue(new WrestlerIntent(Vector2.zero, ButtonState.None, ButtonState.None, new ButtonState(false, true)));
            h.Tick(1, Dt);
            Assert.AreEqual(before - Dt, h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        // COMBAT_SPEC: mashing can't make the knockdown shorter than the shortest possible knockdown.
        [Test]
        public void Mash_NeverBelowShortestKnockdown()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.mashReductionPerPress = 1f; });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            for (int i = 0; i < 300; i++) h.CpuInput.PressStrikeOnce();
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(h.Tuning.shortestKnockdown, t, 2 * Dt);
        }

        // D-025: knocked down wrestlers can't be hit by combo strikes; no invulnerability after getting up.
        [Test]
        public void KnockedDown_ComboStrikeDoesNotLand_ButLandsRightAfterGettingUp()
        {
            using var h = new MatchHarness(P, P + new Vector2(2.6f, 0f), configure: t => t.maxHealth = 32f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            h.TickSeconds(0.3f, Dt);
            // 2.6 apart: outside stomp range (2.0), inside punch reach, so this is a combo strike.
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(1, h.Hits.Count, "strike on a Knocked Down wrestler must not land");
            Assert.AreEqual(MoveId.Strike1, h.MovesStarted.Last().move);

            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(2, h.Hits.Count, "no invulnerability after getting up");
        }

        [Test]
        public void KnockoutAtZero_RaisesMatchEndedOnce_AndFreezes()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 3f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.AreEqual(0f, h.Cpu.Health);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(1, h.MatchEnds.Count);
            Assert.AreEqual(WrestlerId.Player, h.MatchEnds[0].Winner);
            Assert.AreEqual(WrestlerId.Cpu, h.MatchEnds[0].Loser);
            Assert.AreEqual(h.Match.ElapsedSeconds, h.MatchEnds[0].ElapsedSeconds, 1e-6f);
            Assert.AreEqual(h.MatchEnds[0].Winner, h.Match.Result.Winner);
            Assert.IsEmpty(h.KnockDowns, "a knockout is not a knockdown");

            float elapsed = h.Match.ElapsedSeconds;
            Vector2 p = h.Player.Position;
            CombatState ps = h.Player.State;
            int stateEvents = h.StateChanges.Count, phaseEvents = h.PhaseChanges.Count;
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(elapsed, h.Match.ElapsedSeconds, "timer stops at knockout");
            V2Assert.AreEqual(p, h.Player.Position, 1e-6f);
            Assert.AreEqual(ps, h.Player.State);
            Assert.AreEqual(stateEvents, h.StateChanges.Count);
            Assert.AreEqual(phaseEvents, h.PhaseChanges.Count);
            Assert.AreEqual(1, h.MatchEnds.Count);
        }

        [Test]
        public void Knockout_PlayerLoses_CpuWins()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 3f);
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.AreEqual(WrestlerId.Cpu, h.Match.Result.Winner);
            Assert.AreEqual(CombatState.KnockedOut, h.Player.State);
        }

        // D-030: both knocked out on the same tick is a draw; MatchEnded fires once.
        [Test]
        public void DoubleKnockout_SameTick_IsDraw()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 3f);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Player.State);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.IsTrue(h.Match.Result.IsDraw);
            Assert.AreEqual(1, h.MatchEnds.Count);
            Assert.IsTrue(h.MatchEnds[0].IsDraw);
            Assert.AreEqual(h.Match.ElapsedSeconds, h.MatchEnds[0].ElapsedSeconds, 1e-6f);
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(1, h.MatchEnds.Count);
        }

        [Test]
        public void DoubleKnockout_RunCollision_IsDraw()
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f), configure: t => { t.maxHealth = 5f; t.runCollisionDamage = 5f; });
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.IsTrue(h.Match.Result.IsDraw);
            Assert.AreEqual(1, h.MatchEnds.Count);
        }

        [Test]
        public void SingleKnockout_IsNotDraw()
        {
            using var h = new MatchHarness(P, C, configure: t => t.maxHealth = 3f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.IsFalse(h.Match.Result.IsDraw);
        }

        [Test]
        public void HealthNeverBelowZero_DamageReportsWhatWasRemoved()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 2f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            Assert.AreEqual(0f, h.Cpu.Health);
            Assert.AreEqual(2f, h.Hits[0].Damage, 1e-5f);
        }

        [Test]
        public void TuningEdit_Damage_AppliesToNextHit()
        {
            using var h = new MatchHarness(P, C);
            h.Tuning.strike1Damage = 11f;
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(h.Tuning.maxHealth - 11f, h.Cpu.Health, 1e-4f);
        }
    }
}
