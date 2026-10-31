using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// QA coverage additions for the combat-core slice, written from COMBAT_SPEC, CONTROLS and D-024 to D-030
    /// (not from the code). Each test names the rule it checks. Values come from the CombatTuning instance;
    /// the only hard-coded numbers are in <see cref="TuningDefaultsTests"/>, which checks the spec's table itself.
    /// </summary>
    public class StrikeAllowedStatesTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        // COMBAT_SPEC Striking/Combo Rules, "States where a Strike is allowed" (Wrestler B column):
        // Standing, Walking, Running, Striking, Lock Up, Recovering, Struck (D-024). Knocked Down is Stomp only
        // (D-025), which Combat checks separately; every other state can't be struck.
        [TestCase(CombatState.Standing, true)]
        [TestCase(CombatState.Walking, true)]
        [TestCase(CombatState.Running, true)]
        [TestCase(CombatState.Striking, true)]
        [TestCase(CombatState.Struck, true)]
        [TestCase(CombatState.LockUp, true)]
        [TestCase(CombatState.Recovering, true)]
        [TestCase(CombatState.KnockedDown, false)]
        [TestCase(CombatState.LockedUp, false)]
        [TestCase(CombatState.Grappling, false)]
        [TestCase(CombatState.Grappled, false)]
        [TestCase(CombatState.KnockedOut, false)]
        public void CanBeStruck_MatchesSpecList(CombatState defenderState, bool allowed)
        {
            Assert.AreEqual(allowed, CombatMatch.CanBeStruck(defenderState));
        }

        // COMBAT_SPEC: Struck is "Wrestler B only", so a Struck wrestler can't throw a strike; it also can't walk.
        [Test]
        public void Struck_CannotStrikeOrMove()
        {
            using var h = new MatchHarness(P, C, configure: t => t.struckDuration = 0.5f);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Vector2 at = h.Cpu.Position;
            h.CpuInput.HeldMove = Vector2.right;
            h.CpuInput.PressStrikeOnce();
            h.Tick(10, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Assert.IsFalse(h.MovesStarted.Any(m => m.id == WrestlerId.Cpu), "a Struck wrestler must not start a strike");
            V2Assert.AreEqual(at, h.Cpu.Position, 1e-6f, "a Struck wrestler must not move");
        }

        // COMBAT_SPEC Knocked Down: Strike presses while down are mashing, not strikes; a downed wrestler doesn't walk.
        [Test]
        public void KnockedDown_StrikeIsMashNotAMove_AndCannotMove()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 3f; t.maxKnockdownTime = 3f; });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            Vector2 at = h.Cpu.Position;
            h.CpuInput.HeldMove = Vector2.right;
            h.CpuInput.PressStrikeOnce();
            h.Tick(10, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.IsFalse(h.MovesStarted.Any(m => m.id == WrestlerId.Cpu), "a press while Knocked Down must not start a strike");
            V2Assert.AreEqual(at, h.Cpu.Position, 1e-6f, "a Knocked Down wrestler must not move");
        }

        // COMBAT_SPEC Striking: "The first Strike to land ... registers. The other wrestler's Strike is canceled
        // and they enter a Struck state."
        [Test]
        public void FirstStrikeToLand_LoserIsCancelledAndStruck()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.Wait(); h.CpuInput.Wait(); h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(WrestlerId.Player, h.Hits[0].Attacker);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State, "the cancelled striker enters Struck");
            Assert.AreEqual(MoveId.None, h.Cpu.CurrentMove, "the cancelled strike is gone");
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(1, h.Hits.Count, "the cancelled strike never lands later");
        }
    }

    /// <summary>COMBAT_SPEC Striking/Combo Rules, CONTROLS X button, D-029 (Strike from a run), D-030 (window from the press).</summary>
    public class ComboTimingTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 FarCpu = new Vector2(25f, -13f); // out of reach: every strike whiffs
        private const float Dt = 0.01f;

        private static MoveId[] PlayerMoves(MatchHarness h) =>
            h.MovesStarted.Where(m => m.id == WrestlerId.Player).Select(m => m.move).ToArray();

        // CONTROLS: "X pressed a third time within combo time window of second press = Strike 3"; D-030: "the combo
        // window is measured from the previous strike's press". Second press at 0.05 s is buffered during Strike 1;
        // the third press at 0.50 s is 0.45 s after it: inside the window.
        [Test]
        public void BufferedCombo_ThirdPressInsideWindowOfSecondPress_IsStrike3()
        {
            using var h = new MatchHarness(P, FarCpu);
            h.PlayerInput.StrikeOnTicks(200, 0, 5, 50);
            h.TickSeconds(2f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 }, PlayerMoves(h));
        }

        // Same rule, other side: the third press at 0.60 s is 0.55 s after the second press (0.05 s), outside the
        // 0.5 s window, so it must not continue the combo to Strike 3.
        // KNOWN FAILURE (reported): Combat restarts the window when the buffered Strike 2 *starts* (0.46 s), not at
        // the second press, so this press is buffered and Strike 3 is thrown.
        [Test]
        public void BufferedCombo_ThirdPressOutsideWindowOfSecondPress_IsNotStrike3()
        {
            using var h = new MatchHarness(P, FarCpu);
            h.PlayerInput.StrikeOnTicks(200, 0, 5, 60);
            h.TickSeconds(2f, Dt);
            MoveId[] moves = PlayerMoves(h);
            Assert.AreEqual(MoveId.Strike2, moves[1]);
            Assert.IsFalse(moves.Contains(MoveId.Strike3),
                $"D-030/CONTROLS: third press 0.55 s after the second press is outside the {h.Tuning.comboWindow} s window; moves were {string.Join(", ", moves)}");
        }

        // COMBAT_SPEC: a press outside the combo window does not continue the combo. A press during Strike 1 but
        // after the window has closed must not produce Strike 2. (What it should do instead is a spec gap.)
        [Test]
        public void PressDuringStrike_AfterWindowClosed_DoesNotContinueCombo()
        {
            using var h = new MatchHarness(P, FarCpu, configure: t => t.comboWindow = 0.2f);
            Assert.Less(h.Tuning.comboWindow, h.Tuning.strike1Timing.Total, "setup: the window must close before Strike 1 ends");
            h.PlayerInput.StrikeOnTicks(150, 0, 30);
            h.TickSeconds(1.5f, Dt);
            Assert.IsFalse(PlayerMoves(h).Contains(MoveId.Strike2));
        }

        // COMBAT_SPEC lists Strike 1, 2, 3 and no fourth strike: after Strike 3 the next press starts at Strike 1.
        [Test]
        public void AfterStrike3_NextPressInsideWindow_IsStrike1()
        {
            using var h = new MatchHarness(P, FarCpu, configure: t => t.comboWindow = 0.8f);
            // Each press comes after the previous strike has ended, inside the (widened) window.
            h.PlayerInput.StrikeOnTicks(260, 0, 50, 100, 150);
            h.TickSeconds(2.6f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3, MoveId.Strike1 }, PlayerMoves(h));
        }

        // D-029: Strike while running "ends the run and throws Strike 1", even if a combo window is still open
        // (window widened so it is clearly still open when the run's Strike is pressed).
        [Test]
        public void StrikeFromRun_InsideComboWindow_IsStrike1()
        {
            using var h = new MatchHarness(P, FarCpu, configure: t => t.comboWindow = 0.8f);
            h.PlayerInput.StrikeOnTicks(1, 0);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.PlayerInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.Less(h.Match.ElapsedSeconds, h.Tuning.comboWindow, "setup: still inside the window of the first press");
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike1 }, PlayerMoves(h));
        }
    }

    /// <summary>COMBAT_SPEC Knocked Down and Tuning Values (mashing), D-025 (Stomp).</summary>
    public class KnockdownMashAndStompTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        // COMBAT_SPEC: mashing can't shorten a knockdown below "Shortest possible knockdown", which is not the
        // "Minimum knockdown time" (the length at full health). A full-health Strike 3 knockdown, mashed on every
        // tick, ends at the shortest possible knockdown.
        [Test]
        public void Mash_FullHealthKnockdown_EndsAtShortestKnockdown_NotMinimumKnockdownTime()
        {
            using var h = new MatchHarness(P, C);
            Assert.Less(h.Tuning.shortestKnockdown, h.Tuning.minKnockdownTime, "setup: defaults keep the two values apart");
            h.CpuIdle = false;
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            for (int i = 0; i < 400; i++) h.CpuInput.PressStrikeOnce();
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
            Assert.AreEqual(h.Tuning.shortestKnockdown, t, 2 * Dt);
        }

        // COMBAT_SPEC: each press removes the tuned share of the *remaining* time (compounding), so two presses
        // on consecutive ticks leave remaining * (1 - share)^2 (minus the time that passed).
        [Test]
        public void Mash_PressesCompoundOnRemainingTime()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 3f; t.maxKnockdownTime = 3f; });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            float r = h.CpuW.KnockdownTimeRemaining;
            float k = 1f - h.Tuning.mashReductionPerPress;
            h.CpuInput.PressStrikeOnce();
            h.CpuInput.PressStrikeOnce();
            h.Tick(2, Dt);
            Assert.AreEqual(((r - Dt) * k - Dt) * k, h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        // D-025: "There is no limit on stomps per knockdown" (only the cooldown between them).
        [Test]
        public void Stomp_NoLimitPerKnockdown()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            for (int n = 1; n <= 3; n++)
            {
                h.TickUntil(() => h.Player.State == CombatState.Standing && h.PlayerW.StompCooldownRemaining <= 0f, 3f, Dt);
                h.PlayerInput.PressStrikeOnce();
                h.TickUntil(() => h.Hits.Count(x => x.Move == MoveId.Stomp) == n, 1f, Dt);
            }
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(1, h.KnockDowns.Count, "still the same knockdown");
        }

        // D-025: the stomp cooldown starts when a stomp ends; read from tuning at use (D-005).
        [Test]
        public void StompCooldown_TuningEditMidMatch_AppliesToNextStomp()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.Tuning.stompCooldown = 2.5f;
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 2f, Dt);
            Assert.AreEqual(2.5f, h.PlayerW.StompCooldownRemaining, 2 * Dt);
        }
    }

    /// <summary>COMBAT_SPEC Recovering / Damage: "standing still" means no move input (D-030).</summary>
    public class RecoveringStillnessTests
    {
        private const float Dt = 0.01f;

        // D-030: pushing the stick into an obstacle (here the opponent) is not standing still, even though the
        // wrestler doesn't move, so no regeneration.
        [Test]
        public void Recovering_PushingIntoOpponent_DoesNotRegenerate()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f),
                configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; });
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Recovering, 2f, Dt);
            float hp = h.Player.Health;
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(2f, Dt);
            Assert.AreEqual(h.Cpu.Position.x - 2f * h.Tuning.feetHalfWidth, h.Player.Position.x, 0.01f, "setup: blocked against the CPU");
            Assert.AreEqual(hp, h.Player.Health, 1e-5f, "holding a direction is not standing still");
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
        }
    }

    /// <summary>
    /// COMBAT_SPEC Tuning Values / D-005 / ARCHITECTURE 5: values are read at the moment of use, so edits made
    /// after the match is built (Play Mode Inspector edits) apply on the next use.
    /// </summary>
    public class TuningAtUseTimeTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        [Test]
        public void StruckDuration_EditAppliesToNextHit()
        {
            using var h = new MatchHarness(P, C);
            h.Tuning.struckDuration = 0.9f;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(0.9f, h.CpuW.StruckTimeRemaining, 1e-4f);
        }

        [Test]
        public void ComboWindow_EditAppliesMidMatch()
        {
            using var h = new MatchHarness(P, new Vector2(25f, -13f));
            h.Tuning.comboWindow = 1.2f;
            h.PlayerInput.StrikeOnTicks(150, 0, 100);
            h.TickSeconds(1.5f, Dt);
            Assert.AreEqual(MoveId.Strike2, h.MovesStarted[1].move);
        }

        [Test]
        public void RecoveringThreshold_EditAppliesToNextHit()
        {
            using var h = new MatchHarness(P, C);
            h.Tuning.recoveringThreshold = h.Tuning.maxHealth - 1f; // Strike 1 now drops below the threshold
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
        }

        [Test]
        public void KnockdownTimes_EditAppliesToNextKnockdown()
        {
            using var h = new MatchHarness(P, C);
            h.Tuning.minKnockdownTime = 2f;
            h.Tuning.maxKnockdownTime = 2f;
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickUntil(() => h.KnockDowns.Count > 0, 2f, Dt);
            Assert.AreEqual(2f, h.KnockDowns[0].duration, 1e-4f);
        }

        [Test]
        public void MashReduction_EditAppliesToNextPress()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 3f; t.maxKnockdownTime = 3f; });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.KnockDowns.Count > 0, 1f, Dt);
            h.Tuning.mashReductionPerPress = 0.5f;
            float before = h.CpuW.KnockdownTimeRemaining;
            h.CpuInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual((before - Dt) * 0.5f, h.CpuW.KnockdownTimeRemaining, 1e-4f);
        }

        [Test]
        public void RecoveryRate_EditAppliesWhileRecovering()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; });
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Recovering, 2f, Dt);
            h.Tuning.recoveryRate = 0.25f;
            float hp = h.Player.Health;
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(hp + 0.25f, h.Player.Health, 0.01f);
        }

        [Test]
        public void ObstacleDamage_EditAppliesToNextCrash()
        {
            using var h = new MatchHarness(new Vector2(12f, SliceArena.ObstacleLaneY), new Vector2(1f, -13f));
            h.Tuning.obstacleCollisionDamage = 15f;
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(h.Tuning.maxHealth - 15f, h.Player.Health, 1e-4f);
        }

        [Test]
        public void StompDamage_EditAppliesToNextStomp()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 20f; t.maxKnockdownTime = 20f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            h.Tuning.stompDamage = 7f;
            float hp = h.Cpu.Health;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Any(x => x.Move == MoveId.Stomp), 1f, Dt);
            Assert.AreEqual(hp - 7f, h.Cpu.Health, 1e-4f);
        }
    }

    /// <summary>CLAUDE.md / COMBAT_SPEC CPU AI: the CPU's inputs go through the same combat rules as the player's.</summary>
    public class SharedCombatRulesTests
    {
        private const float Dt = 0.01f;

        // A full combo thrown by the player at the CPU, and the same intents thrown by the CPU at the player from
        // the mirrored spot, give the same damage, knockdown and timing.
        [Test]
        public void Combo_PlayerOnCpu_EqualsCpuOnPlayer()
        {
            Vector2 attacker = new Vector2(10f, -12f), defender = new Vector2(12f, -12f);
            using var a = new MatchHarness(attacker, defender);
            using var b = new MatchHarness(defender, attacker);
            b.CpuIdle = false;
            a.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            b.CpuInput.StrikeOnTicks(200, 0, 15, 60);
            a.TickSeconds(2f, Dt);
            b.TickSeconds(2f, Dt);
            Assert.AreEqual(a.Cpu.Health, b.Player.Health, 1e-5f);
            Assert.AreEqual(a.Cpu.State, b.Player.State);
            Assert.AreEqual(a.KnockDowns.Single().duration, b.KnockDowns.Single().duration, 1e-5f);
            CollectionAssert.AreEqual(a.Hits.Select(x => x.Move).ToArray(), b.Hits.Select(x => x.Move).ToArray());
            Assert.IsTrue(b.Hits.All(x => x.Attacker == WrestlerId.Cpu && x.Defender == WrestlerId.Player));
        }
    }

    /// <summary>
    /// COMBAT_SPEC Tuning Values table and D-024/D-025/D-029: a new CombatTuning carries the documented defaults.
    /// These are the only hard-coded combat numbers in the suite on purpose: they check the table itself. The
    /// project asset may be tuned away from them and is not checked.
    /// </summary>
    public class TuningDefaultsTests
    {
        [Test]
        public void NewTuning_HasCombatSpecDefaults()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                const float e = 1e-5f;
                // Timing
                Assert.AreEqual(0.5f, t.comboWindow, e, "Combo window");
                Assert.AreEqual(0.4f, t.struckDuration, e, "Struck duration");
                AssertTiming(t.strike1Timing, 0.21f, 0.1f, 0.15f, "Strike 1");
                AssertTiming(t.strike2Timing, 0.21f, 0.1f, 0.15f, "Strike 2");
                AssertTiming(t.strike3Timing, 0.125f, 0.1f, 0.25f, "Strike 3");
                // Health and Damage
                Assert.AreEqual(100f, t.maxHealth, e, "Max health");
                Assert.AreEqual(30f, t.recoveringThreshold, e, "Recovering threshold");
                Assert.AreEqual(3f, t.strike1Damage, e, "Strike 1 damage");
                Assert.AreEqual(4f, t.strike2Damage, e, "Strike 2 damage");
                Assert.AreEqual(6f, t.strike3Damage, e, "Strike 3 damage");
                Assert.AreEqual(8f, t.obstacleCollisionDamage, e, "Obstacle collision damage");
                Assert.AreEqual(3f, t.stompDamage, e, "Stomp damage");
                Assert.AreEqual(0f, t.runCollisionDamage, e, "Run-collision damage");
                Assert.AreEqual(3.0f, t.walkSpeedHorizontal, e, "Walk speed horizontal");
                Assert.AreEqual(2.0f, t.walkSpeedDepth, e, "Walk speed vertical");
                Assert.AreEqual(3.6f, t.walkSpeedDiagonal, e, "Walk speed diagonal");
                Assert.AreEqual(1.0f, t.stompCooldown, e, "Stomp cooldown");
                // Recovery and Knockdown
                Assert.AreEqual(1f, t.recoveryRate, e, "Recovery rate");
                Assert.AreEqual(30f, t.recoveryCap, e, "Recovery cap");
                Assert.AreEqual(1.0f, t.minKnockdownTime, e, "Minimum knockdown time");
                Assert.AreEqual(3.0f, t.maxKnockdownTime, e, "Maximum knockdown time");
                Assert.AreEqual(0.08f, t.mashReductionPerPress, e, "Mash reduction per press");
                Assert.AreEqual(0.5f, t.shortestKnockdown, e, "Shortest possible knockdown");
                Assert.AreEqual(3f, t.cpuMashRate, e, "CPU mash rate");
                // D-030: CPU strike-back chance while Recovering starts at 30%.
                Assert.AreEqual(0.3f, t.cpuRecoverCounterChance, e, "CPU Recovering strike-back chance");
            }
            finally { Object.DestroyImmediate(t); }
        }

        private static void AssertTiming(MoveTiming m, float startup, float active, float recovery, string name)
        {
            Assert.AreEqual(startup, m.Startup, 1e-5f, name + " wind-up");
            Assert.AreEqual(active, m.Active, 1e-5f, name + " hit window");
            Assert.AreEqual(recovery, m.Recovery, 1e-5f, name + " recovery");
        }
    }
}
