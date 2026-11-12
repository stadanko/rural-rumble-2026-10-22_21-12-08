using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// COMBAT_SPEC Striking/Combo Rules, D-024 (timing, Struck, rooted), D-029 (knockdown check after damage).
    /// Player at (10, -12) facing right; idle CPU 2 units to the right, inside punch and kick reach.
    /// </summary>
    public class StrikeTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(12f, -12f);
        private const float Dt = 0.01f;

        [Test]
        public void Strike1_LandsWhenWindUpEnds_DamagesAndStruck()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
            Assert.AreEqual(MovePhase.Startup, h.Player.Phase);
            Assert.AreEqual((WrestlerId.Player, MoveId.Strike1), (h.MovesStarted[0].id, h.MovesStarted[0].move));
            Assert.AreEqual(h.Tuning.strike1Timing.Startup, h.MovesStarted[0].timing.Startup, 1e-6f);

            float t = h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.That(t, Is.InRange(h.Tuning.strike1Timing.Startup - Dt, h.Tuning.strike1Timing.Startup + 2 * Dt));
            HitInfo hit = h.Hits[0];
            Assert.AreEqual(WrestlerId.Player, hit.Attacker);
            Assert.AreEqual(WrestlerId.Cpu, hit.Defender);
            Assert.AreEqual(MoveId.Strike1, hit.Move);
            Assert.AreEqual(h.Tuning.strike1Damage, hit.Damage, 1e-5f);
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.strike1Damage, h.Cpu.Health, 1e-5f);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Assert.IsTrue(h.HealthChanges.Contains((WrestlerId.Cpu, h.Cpu.Health)));
        }

        [Test]
        public void Strike_PhasesRunStartupActiveRecoveryNone_ThenStanding()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(1f, Dt);
            var phases = h.PhaseChanges.Where(e => e.id == WrestlerId.Player).Select(e => e.phase).ToArray();
            CollectionAssert.AreEqual(new[] { MovePhase.Active, MovePhase.Recovery, MovePhase.None }, phases);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
        }

        [Test]
        public void Strike_TotalLengthMatchesTiming()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            float t = h.TickUntil(() => h.Player.State != CombatState.Striking, 2f, Dt);
            Assert.AreEqual(h.Tuning.strike1Timing.Total, t, 2 * Dt);
        }

        [Test]
        public void Struck_LastsStruckDuration_ThenStanding()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            h.TickSeconds(h.Tuning.struckDuration - 3 * Dt, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            h.TickSeconds(4 * Dt, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }

        // D-024: each hit restarts Struck; StateChanged does not fire for the restart.
        [Test]
        public void Struck_RestartedByNextComboHit_NoRepeatStateChanged()
        {
            using var h = new MatchHarness(P, C, configure: t => t.struckDuration = 0.6f);
            h.PlayerInput.StrikeOnTicks(150, 0, 15);
            h.TickUntil(() => h.Hits.Count >= 2, 2f, Dt);
            Assert.AreEqual(MoveId.Strike2, h.Hits[1].Move);
            Assert.AreEqual(1, h.StateChanges.Count(e => e.id == WrestlerId.Cpu && e.to == CombatState.Struck));
            Assert.AreEqual(h.Tuning.struckDuration, h.CpuW.StruckTimeRemaining, 2 * Dt);
        }

        [Test]
        public void Combo_ThreePresses_Strike123_KnocksDown()
        {
            using var h = new MatchHarness(P, C);
            // Second press during Strike 1 (buffered), third during Strike 2.
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickSeconds(1.5f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 },
                h.MovesStarted.Where(e => e.id == WrestlerId.Player).Select(e => e.move).ToArray());
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 }, h.Hits.Select(x => x.Move).ToArray());
            float expectedHealth = h.Tuning.maxHealth - h.Tuning.strike1Damage - h.Tuning.strike2Damage - h.Tuning.strike3Damage;
            Assert.AreEqual(expectedHealth, h.Cpu.Health, 1e-4f);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(1, h.KnockDowns.Count);
        }

        [Test]
        public void Combo_PressAfterStrikeEnds_InsideWindow_ThrowsStrike2()
        {
            using var h = new MatchHarness(P, C);
            // Strike 1 lasts 0.46 s; press at 0.48 s (inside the 0.5 s window, measured from the first press).
            h.PlayerInput.StrikeOnTicks(100, 0, 48);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(MoveId.Strike2, h.MovesStarted[1].move);
        }

        [Test]
        public void Combo_PressOutsideWindow_StartsAtStrike1Again()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.StrikeOnTicks(150, 0, 100);
            h.TickSeconds(1.5f, Dt);
            Assert.AreEqual(2, h.MovesStarted.Count);
            Assert.AreEqual(MoveId.Strike1, h.MovesStarted[1].move);
        }

        [Test]
        public void ComboWindow_IsTunable()
        {
            using var h = new MatchHarness(P, C, configure: t => t.comboWindow = 1.2f);
            h.PlayerInput.StrikeOnTicks(150, 0, 100);
            h.TickSeconds(1.5f, Dt);
            Assert.AreEqual(MoveId.Strike2, h.MovesStarted[1].move);
        }

        // D-024: wrestlers can't move while striking.
        [Test]
        public void Striking_IsRooted()
        {
            using var h = new MatchHarness(P, new Vector2(20f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Vector2 at = h.Player.Position;
            h.TickSeconds(h.Tuning.strike1Timing.Total - 3 * Dt, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            V2Assert.AreEqual(at, h.Player.Position, 1e-6f);
            h.TickSeconds(0.2f, Dt);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
        }

        // Updated for D-037 (Focus). Old setup (walk left to face away, then strike) now strikes toward the CPU,
        // so "hits only in front" is checked with facing locked by the strike itself: the CPU runs behind the
        // player during a lengthened wind-up (another depth inside strike tolerance) and stops within reach
        // behind; the strike still reaches in its locked direction and whiffs.
        [Test]
        public void Strike_OpponentBehind_FacingLocked_Whiffs()
        {
            // The CPU passes at a depth just beyond D-027 wrestler blocking; that is outside the default strike
            // depth tolerance, so this test widens the tolerance to keep the CPU inside the strike's depth band.
            float gap;
            {
                var d = ScriptableObject.CreateInstance<CombatTuning>();
                gap = 2f * d.wrestlerBlockHalfDepth + 0.1f;
                Object.DestroyImmediate(d);
            }
            using var h = new MatchHarness(P, new Vector2(P.x + 4.5f, P.y + gap), configure: t =>
            {
                t.strike1Timing = new MoveTiming(1.5f, 0.1f, 0.15f);
                t.strikeDepthTolerance = gap + 0.2f;
            });
            Assert.That(4.5f, Is.GreaterThan(h.Tuning.strike1Reach + h.Tuning.hurtHalfWidth), "precondition: CPU starts out of reach");
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.PressRunOnce(); // nothing held: toward the player
            h.Tick(1, Dt);
            float behind = h.Tuning.hurtHalfWidth + 0.5f; // hurt box fully behind the attacker's feet, inside reach if turned
            Assert.That(behind, Is.LessThan(h.Tuning.strike1Reach + h.Tuning.hurtHalfWidth));
            h.TickUntil(() => h.Cpu.Position.x < P.x - behind, 2f, Dt);
            h.CpuInput.PressRunOnce(); // cancel the run (D-027)
            h.TickUntil(() => h.Player.State != CombatState.Striking, 3f, Dt);
            Assert.That(h.Cpu.Position.x, Is.InRange(P.x - behind - 0.3f, P.x - behind), "precondition: CPU stopped behind, in reach if turned");
            Assert.IsEmpty(h.Hits);
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health);
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void Strike_DepthTolerance(float side)
        {
            float tol = 0.4f;
            using (var h = new MatchHarness(P, C + new Vector2(0f, side * (tol - 0.05f)), configure: t => t.strikeDepthTolerance = tol))
            {
                h.PlayerInput.PressStrikeOnce();
                h.TickSeconds(0.5f, Dt);
                Assert.AreEqual(1, h.Hits.Count, "inside depth tolerance");
            }
            using (var h = new MatchHarness(P, C + new Vector2(0f, side * (tol + 0.05f)), configure: t => t.strikeDepthTolerance = tol))
            {
                h.PlayerInput.PressStrikeOnce();
                h.TickSeconds(0.5f, Dt);
                Assert.IsEmpty(h.Hits, "outside depth tolerance");
            }
        }

        // Reach: hit box from the feet point to feet + facing * reach, against the hurt box (feet +/- hurtHalfWidth).
        [Test]
        public void Strike1_Reach_PunchReachPlusHurtHalfWidth()
        {
            using (var h = new MatchHarness(P, P + new Vector2(3.0f, 0f)))
            {
                h.PlayerInput.PressStrikeOnce();
                h.TickSeconds(0.5f, Dt);
                Assert.AreEqual(1, h.Hits.Count, "3.0 apart is inside 2.4 + 0.7");
            }
            using (var h = new MatchHarness(P, P + new Vector2(3.2f, 0f)))
            {
                h.PlayerInput.PressStrikeOnce();
                h.TickSeconds(0.5f, Dt);
                Assert.IsEmpty(h.Hits, "3.2 apart is outside 2.4 + 0.7");
            }
        }

        // COMBAT_SPEC: first strike to land wins; the other strike is cancelled and its thrower is Struck.
        [Test]
        public void BothStrike_EarlierHitWindowWins_OtherCancelled()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.Wait(); h.CpuInput.Wait(); h.CpuInput.Wait(); h.CpuInput.PressStrikeOnce();
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(1, h.Hits.Count);
            Assert.AreEqual(WrestlerId.Player, h.Hits[0].Attacker);
            Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.Strike1, MovePhase.None)), "CPU strike cancelled");
            Assert.IsFalse(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.Strike1, MovePhase.Active)), "CPU strike never became active");
            Assert.AreEqual(h.Tuning.maxHealth, h.Player.Health);
        }

        [Test]
        public void BothStrike_CpuFirst_CpuWins()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.PlayerInput.Wait(); h.PlayerInput.Wait(); h.PlayerInput.Wait(); h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(1, h.Hits.Count);
            Assert.AreEqual(WrestlerId.Cpu, h.Hits[0].Attacker);
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health);
        }

        // Exact same-tick hit windows: a trade (both land). Not specified; see the step-2 report.
        [Test]
        public void BothStrike_SameTick_Trade()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(2, h.Hits.Count);
            Assert.AreEqual(CombatState.Struck, h.Player.State);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
        }

        [Test]
        public void Strike3_KnocksDownAtFullHealth_Strike1DoesNot()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Assert.IsEmpty(h.KnockDowns);
        }

        // COMBAT_SPEC: a strike can land on a Running wrestler; the run ends.
        [Test]
        public void Strike_LandsOnRunningWrestler()
        {
            using var h = new MatchHarness(P, new Vector2(11.2f, -12f));
            h.CpuIdle = false;
            // D-037: the run goes the left/right held on the press (was: turn right with the stick, then Run).
            h.CpuInput.Wait();
            h.CpuInput.HeldMove = Vector2.right;
            h.CpuInput.PressRunOnce();
            h.CpuInput.Release();
            h.PlayerInput.Wait();
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.4f, Dt);
            Assert.AreEqual(1, h.Hits.Count);
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.Running, CombatState.Struck)));
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State, "run does not resume after Struck");
        }

        // A strike connects at most once.
        [Test]
        public void Strike_HitsOnlyOnce()
        {
            using var h = new MatchHarness(P, C, configure: t => t.strike1Timing = new MoveTiming(0.05f, 0.3f, 0.05f));
            h.PlayerInput.PressStrikeOnce();
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(1, h.Hits.Count);
        }

        // D-005: timing read when used.
        [Test]
        public void TuningEdit_StrikeTiming_AppliesToNextStrike()
        {
            using var h = new MatchHarness(P, C);
            h.Tuning.strike1Timing = new MoveTiming(0.05f, 0.1f, 0.05f);
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(0.05f, h.MovesStarted[0].timing.Startup, 1e-6f);
            float t = h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.That(t, Is.LessThan(0.08f));
        }
    }
}
