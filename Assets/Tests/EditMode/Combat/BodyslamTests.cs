using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// Lock-up slice step 2, the Bodyslam: COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules ("Wrestler A presses
    /// the Grapple button": Grapple animation, Wrestler B Knocked Down, damage to B), Grappling / Grappled states,
    /// Bodyslam damage 12, ARCHITECTURE 6 (PairedMoveStarted, Combat owns positions and timing), D-033 (per-pair
    /// timing, both roots stay where the lock-up left them).
    /// Player at (10, -12) facing right; CPU 1.5 units to the right: inside lock-up reach.
    /// </summary>
    public class BodyslamTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(11.5f, -12f);
        private const float Dt = 0.01f;

        /// <summary>Player locks up the CPU, then presses Grapple on the next tick.</summary>
        private static void LockUpAndGrapple(MatchHarness h)
        {
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
        }

        // ================================================================ Start

        [Test]
        public void GrappleInLockUp_StartsBodyslam_GrapplingAndGrappled_RaisesPairedMoveStarted()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 pAt = h.Player.Position, cAt = h.Cpu.Position;

            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);

            Assert.AreEqual(CombatState.Grappling, h.Player.State);
            Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            Assert.AreEqual(MoveId.Bodyslam, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.Bodyslam, h.Cpu.CurrentMove);
            Assert.AreEqual(MovePhase.Startup, h.Cpu.Phase);
            Assert.AreEqual(MoveId.Bodyslam, h.Match.LockUps.CurrentMove);

            PairedMoveInfo info = h.PairedMoves.Last();
            Assert.AreEqual(MoveId.Bodyslam, info.Move);
            Assert.AreEqual(WrestlerId.Player, info.Initiator);
            Assert.AreEqual(WrestlerId.Cpu, info.Receiver);
            Assert.AreEqual(1, info.Facing);
            Assert.AreEqual(h.Tuning.bodyslamTiming.Startup, info.Timing.Startup, 1e-6f);
            Assert.AreEqual(h.Tuning.bodyslamTiming.Total, info.Timing.Total, 1e-6f);
            V2Assert.AreEqual((pAt + cAt) * 0.5f, info.Anchor, 1e-5f, "anchor");

            // D-033: no root moves when the bodyslam starts.
            V2Assert.AreEqual(pAt, h.Player.Position, 1e-6f, "initiator");
            V2Assert.AreEqual(cAt, h.Cpu.Position, 1e-6f, "receiver");

            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.LockUp, CombatState.Grappling)));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.LockedUp, CombatState.Grappled)));
            Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.LockUp, MovePhase.None)), "lock-up ended");
            Assert.IsFalse(h.MovesStarted.Any(m => m.move == MoveId.Bodyslam), "a paired move raises no MoveStarted");
            Assert.AreEqual(0, h.Hits.Count, "no damage before the impact");
        }

        [Test]
        public void GrapplePressedLateInTheLockUpWindow_StillStartsBodyslam()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.TickSeconds(h.Tuning.lockUpTimer - 0.1f, Dt);
            Assert.AreEqual(MoveId.LockUp, h.Player.CurrentMove, "still in the lock-up window");
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappling, h.Player.State);
            Assert.IsFalse(h.PairedMoves.Any(m => m.Move == MoveId.BreakLockUp));
        }

        [Test]
        public void CpuInitiator_SameRules()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappling, h.Cpu.State);
            Assert.AreEqual(CombatState.Grappled, h.Player.State);
            Assert.AreEqual(WrestlerId.Cpu, h.PairedMoves.Last().Initiator);

            h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.bodyslamDamage, h.Player.Health, 1e-4f);
        }

        // ================================================================ Impact and knockdown

        [Test]
        public void Impact_AtBodyslamStartup_AppliesDamage_ReceiverStaysGrappled()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndGrapple(h);

            float t = h.TickUntil(() => h.Hits.Count > 0, 3f, Dt);
            Assert.AreEqual(h.Tuning.bodyslamTiming.Startup, t, 2 * Dt, "impact at the bodyslam Startup");

            HitInfo hit = h.Hits.Single();
            Assert.AreEqual((WrestlerId.Player, WrestlerId.Cpu, MoveId.Bodyslam), (hit.Attacker, hit.Defender, hit.Move));
            Assert.AreEqual(12f, hit.Damage, 1e-5f, "COMBAT_SPEC Bodyslam damage");
            Assert.AreEqual(h.Tuning.maxHealth - 12f, h.Cpu.Health, 1e-4f);
            Assert.IsTrue(h.HealthChanges.Contains((WrestlerId.Cpu, h.Cpu.Health)));

            // Still on the clip: the knockdown starts when the move ends.
            Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            Assert.AreEqual(CombatState.Grappling, h.Player.State);
            Assert.AreEqual(MovePhase.Active, h.Cpu.Phase);
            Assert.AreEqual(0, h.KnockDowns.Count);
        }

        [Test]
        public void MoveEnd_ReceiverKnockedDown_InitiatorStanding_RootsNeverMove()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndGrapple(h);
            Vector2 pAt = h.Player.Position, cAt = h.Cpu.Position;

            float t = 0f;
            while (h.Match.LockUps.IsActive && t < 4f)
            {
                h.Tick(1, Dt);
                t += Dt;
                V2Assert.AreEqual(pAt, h.Player.Position, 1e-6f, "initiator root during the bodyslam");
                V2Assert.AreEqual(cAt, h.Cpu.Position, 1e-6f, "receiver root during the bodyslam");
            }
            Assert.AreEqual(h.Tuning.bodyslamTiming.Total, t, 2 * Dt, "Startup + Active + Recovery");

            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.None, h.Cpu.CurrentMove);
            V2Assert.AreEqual(pAt, h.Player.Position, 1e-6f, "initiator");
            V2Assert.AreEqual(cAt, h.Cpu.Position, 1e-6f, "receiver: not repositioned when the move ends");

            // D-025: knockdown length from the health after the damage.
            float lost = h.Tuning.bodyslamDamage / h.Tuning.maxHealth;
            float expected = Mathf.Lerp(h.Tuning.minKnockdownTime, h.Tuning.maxKnockdownTime, lost);
            var kd = h.KnockDowns.Single();
            Assert.AreEqual(WrestlerId.Cpu, kd.id);
            Assert.AreEqual(expected, kd.duration, 1e-4f);
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.Grappled, CombatState.KnockedDown)));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Grappling, CombatState.Standing)));

            // Phase events for the receiver: LockUp ends (Grapple on the first tick, before its zero-length
            // Startup was ticked), Bodyslam Active (impact) -> Recovery -> None.
            var phases = h.PhaseChanges.Where(e => e.id == WrestlerId.Cpu).Select(e => (e.move, e.phase)).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                (MoveId.LockUp, MovePhase.None),
                (MoveId.Bodyslam, MovePhase.Active), (MoveId.Bodyslam, MovePhase.Recovery), (MoveId.Bodyslam, MovePhase.None)
            }, phases);
        }

        [Test]
        public void AfterBodyslam_InitiatorHasControl_AndCanStompTheDownedReceiver()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndGrapple(h);
            h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);

            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count == 2, 1f, Dt);
            Assert.AreEqual(MoveId.Stomp, h.Hits[1].Move, "Strike over a downed opponent is a Stomp (D-025)");
        }

        [Test]
        public void Bodyslam_LowHealthReceiver_KnockedDownThenRecovering()
        {
            using var h = new MatchHarness(P, C, configure: t =>
            {
                t.maxHealth = 40f; // 40 - 12 = 28: below the Recovering threshold after the slam
                t.recoveryRate = 0f;
                t.minKnockdownTime = t.maxKnockdownTime = t.shortestKnockdown = 0.5f;
            });
            LockUpAndGrapple(h);
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 4f, Dt);
            h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 2f, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Cpu.State);
        }

        [Test]
        public void Bodyslam_ToZeroHealth_KnocksOutAtImpact_EndsTheMatch()
        {
            using var h = new MatchHarness(P, C, configure: tuning => tuning.maxHealth = 12f);
            LockUpAndGrapple(h);
            float t = h.TickUntil(() => h.Match.IsOver, 3f, Dt);

            Assert.AreEqual(h.Tuning.bodyslamTiming.Startup, t, 2 * Dt, "knocked out at the impact");
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(0f, h.Cpu.Health, 1e-6f);
            Assert.AreEqual(0, h.KnockDowns.Count, "Knocked Out, not Knocked Down");
            MatchResult r = h.MatchEnds.Single();
            Assert.IsFalse(r.IsDraw);
            Assert.AreEqual(WrestlerId.Player, r.Winner);
            Assert.IsFalse(h.Match.LockUps.IsActive);
        }

        // ================================================================ No control during the bodyslam

        [Test]
        public void DuringBodyslam_NeitherWrestlerHasControl()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappling, h.Player.State);
            int movesBefore = h.MovesStarted.Count;
            int pairedBefore = h.PairedMoves.Count;

            for (int i = 0; i < 60; i++)
            {
                var mash = new WrestlerIntent(new Vector2(-1f, 1f),
                    new ButtonState(i % 3 == 0, true), new ButtonState(i % 3 == 1, true), new ButtonState(i % 3 == 2, true));
                h.PlayerInput.Enqueue(mash);
                h.CpuInput.Enqueue(mash);
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.Grappling, h.Player.State);
            Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            Assert.AreEqual(movesBefore, h.MovesStarted.Count, "no move started by either");
            Assert.AreEqual(pairedBefore, h.PairedMoves.Count, "no second paired move");
            Assert.AreEqual(1, h.Player.HorizontalFacing);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);
        }

        // ================================================================ Not outside a lock-up

        [Test]
        public void GrapplePressInRange_OutsideALockUp_IsALockUp_NotABodyslam()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State, "the first press locks up");
            Assert.AreEqual(MoveId.LockUp, h.PairedMoves.Single().Move);
            Assert.AreEqual(0, h.Hits.Count);
        }

        [Test]
        public void GrapplePressOutOfRange_Whiffs_NoBodyslam()
        {
            using var h = new MatchHarness(P, new Vector2(16f, -12f));
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            h.PlayerInput.PressGrappleOnce(); // during the whiff
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(0, h.PairedMoves.Count);
            Assert.IsFalse(h.StateChanges.Any(s => s.to == CombatState.Grappling || s.to == CombatState.Grappled));
        }

        [Test]
        public void LockedUpWrestlerPressingGrapple_DoesNotBodyslam()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, Dt);
            for (int i = 0; i < 10; i++)
            {
                h.CpuInput.PressGrappleOnce();
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State, "Wrestler B has no control (D-031)");
        }

        [Test]
        public void GrappleDuringBreak_DoesNotBodyslam()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => h.Match.LockUps.CurrentMove == MoveId.BreakLockUp, 3f, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.BreakLockUp, h.Match.LockUps.CurrentMove, "the window closed when the timer ran out");
            Assert.IsFalse(h.PairedMoves.Any(m => m.Move == MoveId.Bodyslam));
        }

        // ================================================================ Tuning

        [Test]
        public void PerInitiatorStartup_FromTheInitiatorsDefinition()
        {
            var barry = ScriptableObject.CreateInstance<WrestlerDefinition>();
            var peaux = ScriptableObject.CreateInstance<WrestlerDefinition>();
            try
            {
                void Configure(CombatTuning tuning) => tuning.bodyslamStartupByInitiator = new[]
                {
                    new WrestlerStartup(barry, 1.208f), new WrestlerStartup(peaux, 1.083f)
                };

                // Player = Barry slams CPU = Peaux.
                using (var h = new MatchHarness(P, C, configure: Configure, playerWrestler: barry, cpuWrestler: peaux))
                {
                    LockUpAndGrapple(h);
                    Assert.AreEqual(1.208f, h.PairedMoves.Last().Timing.Startup, 1e-6f);
                    float t = h.TickUntil(() => h.Hits.Count > 0, 3f, Dt);
                    Assert.AreEqual(1.208f, t, 2 * Dt);
                }

                // CPU = Barry is slammed by Player = Peaux.
                using (var h = new MatchHarness(P, C, configure: Configure, playerWrestler: peaux, cpuWrestler: barry))
                {
                    LockUpAndGrapple(h);
                    PairedMoveInfo info = h.PairedMoves.Last();
                    Assert.AreEqual(1.083f, info.Timing.Startup, 1e-6f);
                    Assert.AreEqual(h.Tuning.bodyslamTiming.Active, info.Timing.Active, 1e-6f);
                    Assert.AreEqual(h.Tuning.bodyslamTiming.Recovery, info.Timing.Recovery, 1e-6f);
                    float t = h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
                    Assert.AreEqual(1.083f + h.Tuning.bodyslamTiming.Active + h.Tuning.bodyslamTiming.Recovery, t, 2 * Dt);
                }

                // No definitions: the shared bodyslamTiming.
                using (var h = new MatchHarness(P, C, configure: Configure))
                {
                    LockUpAndGrapple(h);
                    Assert.AreEqual(h.Tuning.bodyslamTiming.Startup, h.PairedMoves.Last().Timing.Startup, 1e-6f);
                }
            }
            finally
            {
                Object.DestroyImmediate(barry);
                Object.DestroyImmediate(peaux);
            }
        }

        [Test]
        public void BodyslamDamageAndTiming_AreReadAtUse()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndGrapple(h);
            h.Tuning.bodyslamDamage = 20f; // Play Mode Inspector edits mid-move
            h.Tuning.bodyslamTiming = new MoveTiming(0.5f, 0.1f, 0.2f);
            float t = h.TickUntil(() => h.Hits.Count > 0, 3f, Dt);
            Assert.AreEqual(0.5f, t, 2 * Dt);
            Assert.AreEqual(20f, h.Hits.Single().Damage, 1e-5f);
            float t2 = h.TickUntil(() => !h.Match.LockUps.IsActive, 3f, Dt);
            Assert.AreEqual(0.3f, t2, 2 * Dt);
        }

        [Test]
        public void NewTuning_HasBodyslamDefaults()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                Assert.AreEqual(12f, t.bodyslamDamage, 1e-5f, "COMBAT_SPEC Bodyslam damage");
                Assert.AreEqual(12f, t.DamageFor(MoveId.Bodyslam), 1e-5f);
                Assert.AreEqual(t.bodyslamTiming.Total, t.TimingFor(MoveId.Bodyslam).Total, 1e-6f);
                // D-033: Active + Recovery cover the rest of both approved clips after impact (1.209 s).
                Assert.AreEqual(1.209f, t.bodyslamTiming.Active + t.bodyslamTiming.Recovery, 0.01f);
                Assert.AreEqual(t.bodyslamTiming.Startup, t.BodyslamTimingFor(null).Startup, 1e-6f);
            }
            finally { Object.DestroyImmediate(t); }
        }
    }
}
