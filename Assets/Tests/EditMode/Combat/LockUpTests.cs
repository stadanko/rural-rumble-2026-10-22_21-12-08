using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// Lock-up slice step 1: COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules, D-028 (valid targets, 50/50),
    /// D-031 (whiff, no control for B, A's Strike ignored), ARCHITECTURE 6 (pair anchor, PairedMoveStarted).
    /// Player at (10, -12) facing right; CPU 1.5 units to the right, facing left: inside lock-up reach
    /// (lockUpReach + hurtHalfWidth = 1.7 by default) and strike reach.
    /// </summary>
    public class LockUpTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(11.5f, -12f);
        private const float Dt = 0.01f;

        // ================================================================ Success and payload

        [Test]
        public void LockUp_FromStanding_OnStandingOpponent_SnapsAndRaisesPairedMoveStarted()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);

            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Assert.AreEqual(MoveId.LockUp, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.LockUp, h.Cpu.CurrentMove);
            Assert.IsFalse(h.PlayerW.IsWhiffing);

            // ARCHITECTURE 6: A stays put, B is snapped lockUpPairSpacing in front of A at A's depth; facing each other.
            V2Assert.AreEqual(P, h.Player.Position, 1e-5f, "initiator");
            V2Assert.AreEqual(new Vector2(P.x + h.Tuning.lockUpPairSpacing, P.y), h.Cpu.Position, 1e-5f, "receiver");
            Assert.AreEqual(1, h.Player.HorizontalFacing);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);

            Assert.AreEqual(1, h.PairedMoves.Count);
            PairedMoveInfo info = h.PairedMoves[0];
            Assert.AreEqual(WrestlerId.Player, info.Initiator);
            Assert.AreEqual(WrestlerId.Cpu, info.Receiver);
            Assert.AreEqual(MoveId.LockUp, info.Move);
            Assert.AreEqual(1, info.Facing);
            V2Assert.AreEqual((h.Player.Position + h.Cpu.Position) * 0.5f, info.Anchor, 1e-5f, "anchor");
            Assert.AreEqual(h.Tuning.lockUpTimer, info.Timing.Active, 1e-6f, "the LockUp move's window is the lock-up timer");
            Assert.AreEqual(0f, info.Timing.Startup, 1e-6f);

            // State changes were raised for both; no MoveStarted for a paired move.
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.Standing, CombatState.LockUp)));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.Standing, CombatState.LockedUp)));
            Assert.IsFalse(h.MovesStarted.Any(m => m.move == MoveId.LockUp));
        }

        [Test]
        public void LockUp_PlayerAndCpuRoles_Swapped_SameRules()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Cpu.State);
            Assert.AreEqual(CombatState.LockedUp, h.Player.State);
            Assert.AreEqual(WrestlerId.Cpu, h.PairedMoves.Single().Initiator);
            Assert.AreEqual(-1, h.PairedMoves[0].Facing);
            V2Assert.AreEqual(new Vector2(C.x - h.Tuning.lockUpPairSpacing, C.y), h.Player.Position, 1e-5f, "receiver");
        }

        [Test]
        public void LockUp_FromWalking_OnWalkingOpponent()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.HeldMove = new Vector2(0f, 1f); // CPU walks away from the camera
            h.PlayerInput.HeldMove = new Vector2(1f, 0f); // player walks toward the CPU
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            Assert.AreEqual(CombatState.Walking, h.Cpu.State);

            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Assert.AreEqual(h.Player.Position.y, h.Cpu.Position.y, 1e-5f, "snapped to the initiator's depth");
        }

        [Test]
        public void LockUp_OnRecoveringOpponent()
        {
            using var h = new MatchHarness(P, C, configure: LowHealth);
            MakeCpuRecovering(h);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
        }

        [Test]
        public void LockUp_FromRecovering()
        {
            using var h = new MatchHarness(P, C, configure: LowHealth);
            MakePlayerRecovering(h);
            h.CpuIdle = true;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
        }

        // ================================================================ Lock-up vs. a strike

        [Test]
        public void LockUp_OnOpponentStrikeWindingUp_CancelsTheStrike()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.PlayerInput.Wait();
            h.Tick(1, Dt);
            Assert.AreEqual(MovePhase.Startup, h.Cpu.Phase);

            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.Strike1, MovePhase.None)), "strike cancelled");

            h.TickSeconds(1f, Dt);
            Assert.AreEqual(0, h.Hits.Count, "the cancelled strike never lands");
            Assert.AreEqual(h.Tuning.maxHealth, h.Player.Health, 1e-5f);
        }

        [Test]
        public void LockUp_SameTickOpponentStartsStrike_CancelsTheStrike()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(0, h.Hits.Count);
        }

        [Test]
        public void LockUp_PressedAsOpponentHitWindowOpens_Whiffs_AndTheStrikeLands()
        {
            // Startup = one tick, so the CPU's hit window opens on the tick after its press.
            const float dt = 0.05f;
            using var h = new MatchHarness(P, C, configure: t => t.strike1Timing = new MoveTiming(dt, 0.1f, 0.15f));
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.PlayerInput.Wait();
            h.Tick(1, dt);
            Assert.AreEqual(MovePhase.Startup, h.Cpu.Phase);

            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, dt);

            Assert.AreEqual(0, h.PairedMoves.Count, "a strike whose hit window is open can't be locked up");
            HitInfo hit = h.Hits.Single();
            Assert.AreEqual((WrestlerId.Cpu, WrestlerId.Player, MoveId.Strike1), (hit.Attacker, hit.Defender, hit.Move));
            Assert.AreEqual(CombatState.Struck, h.Player.State, "the whiffing wrestler was hit (C1.9: strikes land on Lock Up)");
            Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Player, MoveId.LockUp, MovePhase.None)), "whiff cut short");
        }

        // ================================================================ Rejections

        [TestCase(CombatState.Standing, MoveId.None, MovePhase.None, true)]
        [TestCase(CombatState.Walking, MoveId.None, MovePhase.None, true)]
        [TestCase(CombatState.Recovering, MoveId.None, MovePhase.None, true)]
        [TestCase(CombatState.Striking, MoveId.Strike1, MovePhase.Startup, true)]
        [TestCase(CombatState.Striking, MoveId.Strike2, MovePhase.Startup, true)]
        [TestCase(CombatState.Striking, MoveId.Strike3, MovePhase.Startup, true)]
        [TestCase(CombatState.Striking, MoveId.Strike1, MovePhase.Active, false)]
        [TestCase(CombatState.Striking, MoveId.Strike1, MovePhase.Recovery, false)]
        [TestCase(CombatState.Striking, MoveId.Stomp, MovePhase.Startup, false)]
        [TestCase(CombatState.Running, MoveId.None, MovePhase.None, false)]
        [TestCase(CombatState.Struck, MoveId.None, MovePhase.None, false)]
        [TestCase(CombatState.LockUp, MoveId.LockUp, MovePhase.Active, false)]
        [TestCase(CombatState.LockedUp, MoveId.LockUp, MovePhase.Active, false)]
        [TestCase(CombatState.Grappling, MoveId.None, MovePhase.None, false)]
        [TestCase(CombatState.Grappled, MoveId.None, MovePhase.None, false)]
        [TestCase(CombatState.KnockedDown, MoveId.None, MovePhase.None, false)]
        [TestCase(CombatState.KnockedOut, MoveId.None, MovePhase.None, false)]
        public void CanBeLockedUp_MatchesSpecList(CombatState state, MoveId move, MovePhase phase, bool expected)
        {
            Assert.AreEqual(expected, LockUpController.CanBeLockedUp(state, move, phase));
        }

        [Test]
        public void LockUp_OnRunningOpponent_Whiffs()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.Enqueue(new WrestlerIntent(new Vector2(1f, 0f), ButtonState.None, ButtonState.None, ButtonState.None)); // turn right
            h.Tick(1, Dt);
            h.CpuInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);

            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);
            Assert.AreEqual(0, h.PairedMoves.Count);
        }

        [Test]
        public void LockUp_OnStruckOpponent_Whiffs()
        {
            using var h = new MatchHarness(P, C, configure: t => t.struckDuration = 3f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Standing && h.Cpu.State == CombatState.Struck, 2f, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
        }

        [Test]
        public void LockUp_OnKnockedDownOpponent_Whiffs()
        {
            using var h = new MatchHarness(P, C, configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 3f; t.maxKnockdownTime = 3f; });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Standing && h.Cpu.State == CombatState.KnockedDown, 2f, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
        }

        [Test]
        public void LockUp_OnWhiffingOpponent_Whiffs()
        {
            using var h = new MatchHarness(P, new Vector2(11.5f, -12f));
            h.CpuIdle = false;
            h.CpuInput.Enqueue(new WrestlerIntent(new Vector2(1f, 0f), ButtonState.None, ButtonState.None, ButtonState.None)); // face away
            h.Tick(1, Dt);
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.CpuW.IsWhiffing);

            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing, "Lock Up (whiffing) is not a valid target");
            Assert.AreEqual(0, h.PairedMoves.Count);
        }

        [Test]
        public void LockUp_OutOfReach_Whiffs()
        {
            using var h = new MatchHarness(P, new Vector2(P.x + 3f, P.y));
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }

        [Test]
        public void LockUp_BeyondDepthTolerance_Whiffs()
        {
            // Default strikeDepthTolerance is 0.4; 0.6 apart in depth is outside it (and not blocked).
            using var h = new MatchHarness(P, new Vector2(C.x, P.y + 0.6f));
            Assert.That(0.6f, Is.GreaterThan(h.Tuning.strikeDepthTolerance));
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
        }

        [Test]
        public void LockUp_FacingAway_Whiffs()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(-1f, 0f), ButtonState.None, ButtonState.None, ButtonState.None)); // turn left
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
        }

        [Test]
        public void LockUp_WhileStruck_DoesNothing()
        {
            using var h = new MatchHarness(P, C, configure: t => t.struckDuration = 3f);
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Struck, 2f, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Struck, h.Player.State);
            Assert.AreEqual(0, h.PairedMoves.Count);
        }

        [Test]
        public void LockUp_WhileStriking_DoesNothing()
        {
            using var h = new MatchHarness(P, new Vector2(P.x + 5f, P.y));
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
        }

        // ================================================================ Whiff (D-031)

        [Test]
        public void Whiff_LastsWhiffTime_Rooted_ThenStanding()
        {
            using var h = new MatchHarness(P, new Vector2(P.x + 5f, P.y));
            h.PlayerInput.HeldMove = new Vector2(0f, 1f); // stick held through the whiff
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 at = h.Player.Position;

            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(MoveId.LockUp, h.Player.CurrentMove);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            var started = h.MovesStarted.Single(m => m.id == WrestlerId.Player);
            Assert.AreEqual(MoveId.LockUp, started.move);
            Assert.AreEqual(h.Tuning.lockUpWhiffTime, started.timing.Total, 1e-6f);
            Assert.AreEqual(0, h.PairedMoves.Count, "a whiff is not a paired move");

            float t = h.TickUntil(() => h.Player.State != CombatState.LockUp, 2f, Dt);
            Assert.AreEqual(h.Tuning.lockUpWhiffTime, t, 2 * Dt, "whiff length from the press tick");
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            V2Assert.AreEqual(at, h.Player.Position, 1e-6f, "rooted during the whiff");
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Walking, h.Player.State, "normal control again (stick still held)");
        }

        [Test]
        public void Whiff_PositionUnchangedThroughout()
        {
            using var h = new MatchHarness(P, new Vector2(P.x + 5f, P.y));
            h.PlayerInput.HeldMove = new Vector2(-1f, -1f);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 at = h.Player.Position;
            while (h.Player.State == CombatState.LockUp)
            {
                V2Assert.AreEqual(at, h.Player.Position, 1e-6f, "rooted");
                h.Tick(1, Dt);
            }
        }

        [Test]
        public void Whiff_ReturnsToRecovering_WhenLowHealth()
        {
            using var h = new MatchHarness(P, C, configure: LowHealth);
            MakePlayerRecovering(h);
            h.CpuIdle = true;
            h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(-1f, 0f), ButtonState.None, ButtonState.None, ButtonState.None)); // face away
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);
            h.TickUntil(() => h.Player.State != CombatState.LockUp, 2f, Dt);
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
        }

        [Test]
        public void Whiff_CanBeStruck()
        {
            // 2.0 apart: outside lock-up reach (1.7) but inside the CPU's punch reach.
            using var h = new MatchHarness(P, new Vector2(P.x + 2f, P.y));
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing);

            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(WrestlerId.Player, h.Hits[0].Defender);
            Assert.AreEqual(CombatState.Struck, h.Player.State);
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
        }

        [Test]
        public void Whiff_TimeIsReadAtUse()
        {
            using var h = new MatchHarness(P, new Vector2(P.x + 5f, P.y));
            h.Tuning.lockUpWhiffTime = 1.0f;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            float t = h.TickUntil(() => h.Player.State != CombatState.LockUp, 3f, Dt);
            Assert.AreEqual(1.0f, t, 2 * Dt);
        }

        // ================================================================ During the lock-up (D-031)

        [Test]
        public void DuringLockUp_LockedUpWrestler_HasNoControl()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Vector2 at = h.Cpu.Position;
            int movesBefore = h.MovesStarted.Count;

            h.CpuInput.HeldMove = new Vector2(1f, 1f);
            for (int i = 0; i < 40; i++)
            {
                h.CpuInput.Enqueue(new WrestlerIntent(new Vector2(1f, 1f),
                    new ButtonState(i % 3 == 0, true), new ButtonState(i % 3 == 1, true), new ButtonState(i % 3 == 2, true)));
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
                V2Assert.AreEqual(at, h.Cpu.Position, 1e-6f, "B can't move");
            }
            Assert.AreEqual(movesBefore, h.MovesStarted.Count, "B started no move");
            Assert.AreEqual(0, h.Hits.Count);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
        }

        [Test]
        public void DuringLockUp_InitiatorStrikeAndMovement_AreIgnored()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 at = h.Player.Position;
            int movesBefore = h.MovesStarted.Count;

            for (int i = 0; i < 40; i++)
            {
                h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(-1f, 1f), ButtonState.None, ButtonState.None, new ButtonState(true, true)));
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.LockUp, h.Player.State);
                Assert.AreEqual(MoveId.LockUp, h.Player.CurrentMove);
                V2Assert.AreEqual(at, h.Player.Position, 1e-6f, "A can't move");
            }
            Assert.AreEqual(movesBefore, h.MovesStarted.Count);
            Assert.AreEqual(0, h.Hits.Count);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "facing unchanged");
        }

        [Test]
        public void DuringLockUp_InitiatorGrappleAndWhipPresses_DoNothingYet()
        {
            // Step 1: Grapple (A) and Irish Whip (B) are hooks only; the lock-up breaks on the timer as usual.
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.PlayerInput.PressRunOnce();
            h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(1f, 0f), ButtonState.None, new ButtonState(true, true), ButtonState.None));
            float t = h.TickUntil(() => h.PairedMoves.Count == 2, 4f, Dt);
            Assert.AreEqual(MoveId.BreakLockUp, h.PairedMoves[1].Move);
            Assert.AreEqual(h.Tuning.lockUpTimer, t, 2 * Dt);
            Assert.AreEqual(CombatState.LockUp, h.Player.State, "no run, no whip, no grapple");
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
        }

        // ================================================================ Timer -> break

        [Test]
        public void Timer_Expires_BreakLockUp_ThenBothStanding()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);

            float t = h.TickUntil(() => h.PairedMoves.Count == 2, 4f, Dt);
            Assert.AreEqual(h.Tuning.lockUpTimer, t, 2 * Dt, "lock-up timer");

            PairedMoveInfo brk = h.PairedMoves[1];
            Assert.AreEqual(MoveId.BreakLockUp, brk.Move);
            Assert.AreEqual(WrestlerId.Player, brk.Initiator);
            Assert.AreEqual(WrestlerId.Cpu, brk.Receiver);
            Assert.AreEqual(h.Tuning.breakLockUpTiming.Total, brk.Timing.Total, 1e-6f);
            Assert.AreEqual(MoveId.BreakLockUp, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.BreakLockUp, h.Cpu.CurrentMove);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Vector2 pAt = h.Player.Position, cAt = h.Cpu.Position;

            float t2 = h.TickUntil(() => h.Player.State != CombatState.LockUp, 2f, Dt);
            Assert.AreEqual(h.Tuning.breakLockUpTiming.Total, t2, 2 * Dt, "break length");
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.None, h.Cpu.CurrentMove);
            Assert.IsFalse(h.Match.LockUps.IsActive);
            V2Assert.AreEqual(pAt, h.Player.Position, 1e-6f);
            V2Assert.AreEqual(cAt, h.Cpu.Position, 1e-6f);

            // Phases for each wrestler: LockUp Active (timer) -> Recovery -> None, Break Active -> Recovery -> None.
            var phases = h.PhaseChanges.Where(e => e.id == WrestlerId.Cpu).Select(e => (e.move, e.phase)).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                (MoveId.LockUp, MovePhase.Active), (MoveId.LockUp, MovePhase.Recovery), (MoveId.LockUp, MovePhase.None),
                (MoveId.BreakLockUp, MovePhase.Active), (MoveId.BreakLockUp, MovePhase.Recovery), (MoveId.BreakLockUp, MovePhase.None)
            }, phases);
        }

        [Test]
        public void Break_ReturnsLowHealthWrestlerToRecovering()
        {
            using var h = new MatchHarness(P, C, configure: LowHealth);
            MakeCpuRecovering(h);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            h.TickUntil(() => !h.Match.LockUps.IsActive, 5f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(CombatState.Recovering, h.Cpu.State);
        }

        [Test]
        public void AfterBreak_BothHaveNormalControl()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => !h.Match.LockUps.IsActive, 5f, Dt);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
        }

        [Test]
        public void LockUpTimer_IsReadAtUse()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.Tuning.lockUpTimer = 0.5f; // edited mid lock-up (Play Mode Inspector edit)
            float t = h.TickUntil(() => h.PairedMoves.Count == 2, 4f, Dt);
            Assert.AreEqual(0.5f, t, 2 * Dt);
        }

        // ================================================================ Simultaneous (D-028)

        [Test]
        public void Simultaneous_InReach_WinnerFromRandomSource([Values(0.2f, 0.8f)] float roll)
        {
            using var h = new MatchHarness(P, C, random01: () => roll);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);

            WrestlerId expected = roll < 0.5f ? WrestlerId.Player : WrestlerId.Cpu;
            Assert.AreEqual(1, h.PairedMoves.Count);
            Assert.AreEqual(expected, h.PairedMoves[0].Initiator);
            Assert.AreEqual(CombatState.LockUp, h.Match.Get(expected).State);
            Assert.AreEqual(CombatState.LockedUp, h.Match.Get(expected == WrestlerId.Player ? WrestlerId.Cpu : WrestlerId.Player).State);
        }

        [Test]
        public void Simultaneous_SeededRandom_IsFiftyFifty_AndRepeatable()
        {
            int playerWins = 0;
            var rngA = new System.Random(2026);
            var outcomes = new WrestlerId[200];
            for (int i = 0; i < outcomes.Length; i++)
            {
                using var h = new MatchHarness(P, C, random01: () => (float)rngA.NextDouble());
                h.CpuIdle = false;
                h.PlayerInput.PressGrappleOnce();
                h.CpuInput.PressGrappleOnce();
                h.Tick(1, Dt);
                outcomes[i] = h.PairedMoves.Single().Initiator;
                if (outcomes[i] == WrestlerId.Player) playerWins++;
            }
            Assert.That(playerWins, Is.InRange(70, 130), "roughly 50/50 over 200 seeded lock-ups");

            var rngB = new System.Random(2026);
            for (int i = 0; i < outcomes.Length; i++)
            {
                using var h = new MatchHarness(P, C, random01: () => (float)rngB.NextDouble());
                h.CpuIdle = false;
                h.PlayerInput.PressGrappleOnce();
                h.CpuInput.PressGrappleOnce();
                h.Tick(1, Dt);
                Assert.AreEqual(outcomes[i], h.PairedMoves.Single().Initiator, $"same seed, same winner ({i})");
            }
        }

        [Test]
        public void Simultaneous_OnlyOneReaches_ThatOneWins_NoRandom()
        {
            // The CPU faces away, so only the player's grab reaches.
            using var h = new MatchHarness(P, C, random01: () => { Assert.Fail("no coin flip needed"); return 0f; });
            h.CpuIdle = false;
            h.CpuInput.Enqueue(new WrestlerIntent(new Vector2(1f, 0f), ButtonState.None, ButtonState.None, ButtonState.None));
            h.PlayerInput.Wait();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(WrestlerId.Player, h.PairedMoves.Single().Initiator);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing, "receiver turned to face the initiator");
        }

        // ================================================================ Snap

        [Test]
        public void Snap_ReceiverSpotOutsideArena_InitiatorIsPlacedInstead()
        {
            // Right end of the band: max feet x = 26.6667 - feetHalfWidth.
            float maxX = SliceArena.BandXMax - 0.5f;
            var p = new Vector2(maxX - 1.2f, -12f);
            using var h = new MatchHarness(p, new Vector2(maxX, -12f), configure: t => t.lockUpPairSpacing = 1.5f);
            Vector2 c = h.Cpu.Position;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            V2Assert.AreEqual(c, h.Cpu.Position, 1e-4f, "receiver stays inside the arena");
            V2Assert.AreEqual(new Vector2(c.x - 1.5f, c.y), h.Player.Position, 1e-4f, "initiator placed lockUpPairSpacing behind");
        }

        // ================================================================ Defaults

        [Test]
        public void NewTuning_HasLockUpDefaults()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                Assert.AreEqual(2.0f, t.lockUpTimer, 1e-5f, "COMBAT_SPEC Set");
                Assert.AreEqual(0.4f, t.lockUpWhiffTime, 1e-5f, "D-031 starting point");
            }
            finally { Object.DestroyImmediate(t); }
        }

        // ================================================================ Helpers

        /// <summary>One Strike 1 (3 damage) takes a 32-health wrestler below 30: knocked down, then Recovering.</summary>
        private static void LowHealth(CombatTuning t)
        {
            t.maxHealth = 32f;
            t.recoveryRate = 0f; // stay Recovering
            t.minKnockdownTime = 0.5f;
            t.maxKnockdownTime = 0.5f;
            t.shortestKnockdown = 0.5f;
        }

        private static void MakeCpuRecovering(MatchHarness h)
        {
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.Recovering && h.Player.State == CombatState.Standing, 3f, Dt);
        }

        private static void MakePlayerRecovering(MatchHarness h)
        {
            h.CpuIdle = false;
            h.CpuInput.PressStrikeOnce();
            h.TickUntil(() => h.Player.State == CombatState.Recovering && h.Cpu.State == CombatState.Standing, 3f, Dt);
        }
    }
}
