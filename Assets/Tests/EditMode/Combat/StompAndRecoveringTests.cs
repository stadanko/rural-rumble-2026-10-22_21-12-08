using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>D-025 Stomp: Strike over a Knocked Down opponent; damage only; timer unchanged; can knock out; cooldown.</summary>
    public class StompTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        /// <summary>CPU knocked down by one Strike 1 (32 -> 29) and kept down for a long time.</summary>
        private static MatchHarness DownedCpu(System.Action<CombatTuning> extra = null)
        {
            var h = new MatchHarness(P, C, configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 10f; t.maxKnockdownTime = 10f;
                extra?.Invoke(t);
            });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            return h;
        }

        [Test]
        public void Strike_OverKnockedDownOpponent_IsStomp_DamageOnly_TimerUnchanged()
        {
            using var h = DownedCpu();
            float health = h.Cpu.Health;
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Player.CurrentMove);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual((WrestlerId.Player, MoveId.Stomp), (h.MovesStarted.Last().id, h.MovesStarted.Last().move));

            float remainingBefore = 0f;
            h.TickUntil(() =>
            {
                if (h.Hits.Count < 2) remainingBefore = h.CpuW.KnockdownTimeRemaining;
                return h.Hits.Count >= 2;
            }, 1f, Dt);
            HitInfo stomp = h.Hits[1];
            Assert.AreEqual(MoveId.Stomp, stomp.Move);
            Assert.AreEqual(h.Tuning.stompDamage, stomp.Damage, 1e-5f);
            Assert.AreEqual(health - h.Tuning.stompDamage, h.Cpu.Health, 1e-4f);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(remainingBefore - Dt, h.CpuW.KnockdownTimeRemaining, 1e-4f, "stomp must not change the knockdown timer");
            Assert.AreEqual(1, h.KnockDowns.Count, "no new knockdown");
        }

        [Test]
        public void Stomp_OutOfRange_IsComboStrikeInstead()
        {
            using var h = DownedCpu(t => t.stompRangeX = 1.5f); // CPU is 2 units away
            h.TickSeconds(h.Tuning.comboWindow, Dt);             // past the combo window: a fresh Strike 1
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(1, h.Hits.Count, "a combo strike can't land on a Knocked Down opponent");
        }

        [Test]
        public void Stomp_Cooldown_StartsWhenStompEnds()
        {
            using var h = DownedCpu();
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 2f, Dt);
            Assert.AreEqual(h.Tuning.stompCooldown, h.PlayerW.StompCooldownRemaining, 2 * Dt);

            int moves = h.MovesStarted.Count;
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(moves, h.MovesStarted.Count, "press during the cooldown does nothing");
            Assert.AreEqual(CombatState.Standing, h.Player.State);

            h.TickSeconds(h.Tuning.stompCooldown, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Player.CurrentMove, "stomp allowed again after the cooldown");
        }

        [Test]
        public void Stomp_CanKnockOut()
        {
            using var h = DownedCpu(t => t.stompDamage = 50f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 2f, Dt);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(WrestlerId.Player, h.Match.Result.Winner);
            Assert.AreEqual(1, h.MatchEnds.Count);
            Assert.AreEqual(MoveId.Stomp, h.Hits.Last().Move);
        }

        [Test]
        public void Stomp_OpponentGetsUpDuringWindUp_Misses()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 0.6f; t.maxKnockdownTime = 0.6f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.TickUntil(() => h.CpuW.KnockdownTimeRemaining < 0.1f, 1f, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.6f, Dt);
            Assert.AreEqual(1, h.Hits.Count, "a stomp only lands on a Knocked Down opponent");
        }
    }

    /// <summary>COMBAT_SPEC Recovering: slowed walk, no running, regenerates only standing still, up to the cap (D-006).</summary>
    public class RecoveringTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        /// <summary>Player knocked down by the CPU's Strike 1 (32 -> 29), then up and Recovering.</summary>
        private static MatchHarness RecoveringPlayer()
        {
            var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; });
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Recovering, 2f, Dt);
            return h;
        }

        [Test]
        public void Recovering_WalksAtReducedSpeed_StaysRecovering()
        {
            using var h = RecoveringPlayer();
            float x = h.Player.Position.x;
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(20, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
            Assert.AreEqual(x - h.Tuning.walkSpeedHorizontal * h.Tuning.recoveringWalkSpeedMultiplier * 20 * Dt, h.Player.Position.x, 1e-4f);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "D-037 Focus: backing away while Recovering still faces the CPU");
        }

        [Test]
        public void Recovering_CannotRun()
        {
            using var h = RecoveringPlayer();
            Vector2 p = h.Player.Position;
            h.PlayerInput.PressRunOnce();
            h.Tick(10, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
            V2Assert.AreEqual(p, h.Player.Position, 1e-6f);
        }

        [Test]
        public void Recovering_RegeneratesOnlyWhileStandingStill()
        {
            using var h = RecoveringPlayer();
            float hp = h.Player.Health;
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(hp, h.Player.Health, 1e-5f, "no regeneration while walking");
            h.PlayerInput.Release();
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(hp + h.Tuning.recoveryRate * 0.5f, h.Player.Health, 0.02f);
            Assert.IsTrue(h.HealthChanges.Any(e => e.id == WrestlerId.Player && e.health > hp));
        }

        [Test]
        public void Recovering_ReachesCap_BecomesStanding_StopsRegenerating()
        {
            using var h = RecoveringPlayer();
            h.TickSeconds(1.2f, Dt); // 29 + 1.2 > 30
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(h.Tuning.recoveryCap, h.Player.Health, 1e-5f);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(h.Tuning.recoveryCap, h.Player.Health, 1e-5f);
        }

        [Test]
        public void Recovering_Struck_IsKnockedDown()
        {
            using var h = RecoveringPlayer();
            int kd = h.KnockDowns.Count;
            h.CpuInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(kd + 1, h.KnockDowns.Count);
        }

        [Test]
        public void Recovering_CanStrike_ReturnsToRecovering()
        {
            using var h = RecoveringPlayer();
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            h.TickUntil(() => h.Player.State != CombatState.Striking, 1f, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
        }
    }
}
