using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuralRumble.AI;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// COMBAT_SPEC CPU AI "Lock Up/Grapple" (lock-up slice step 1): in reach, the CPU presses Lock Up instead
    /// of striking at cpuLockUpChance; while in Lock Up it holds (no grapple yet); while Locked Up it gives no
    /// input (D-031). Its presses go through the same Combat rules as the player's.
    /// </summary>
    public class OpponentBrainLockUpTests
    {
        private const float Dt = 0.02f;

        private sealed class Recording : IWrestlerInput
        {
            private readonly IWrestlerInput inner;
            public readonly List<WrestlerIntent> Reads = new List<WrestlerIntent>();
            public Recording(IWrestlerInput inner) { this.inner = inner; }
            public WrestlerIntent Read() { WrestlerIntent i = inner.Read(); Reads.Add(i); return i; }
        }

        private static (OpponentBrain brain, Recording rec) AttachBrain(MatchHarness h, int seed = 7)
        {
            var brain = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, seed);
            var rec = new Recording(brain);
            h.CpuSource = rec;
            return (brain, rec);
        }

        private static bool IsIdle(WrestlerIntent i) =>
            i.Move == Vector2.zero && !i.Strike.Pressed && !i.RunWhip.Pressed && !i.LockUpGrapple.Pressed;

        [Test]
        public void LockUpChanceCertain_ApproachesAndLocksUpThePlayer()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f;
            });
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.PairedMoves.Count > 0, 10f, Dt);

            Assert.AreEqual(WrestlerId.Cpu, h.PairedMoves[0].Initiator);
            Assert.AreEqual(CombatState.LockUp, h.Cpu.State);
            Assert.AreEqual(CombatState.LockedUp, h.Player.State);
            Assert.AreEqual(OpponentBrain.BrainState.LockUp, brain.State);
            Assert.AreEqual(0, h.Hits.Count, "chose the lock-up instead of striking");
            Assert.AreEqual(1, rec.Reads.Count(i => i.LockUpGrapple.Pressed), "one press, no whiff");
        }

        [Test]
        public void InLockUp_CpuHoldsUntilTheBreak_ThenActsAgain()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f;
            });
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.PairedMoves.Count > 0, 10f, Dt);
            int from = rec.Reads.Count;

            h.TickUntil(() => !h.Match.LockUps.IsActive, 5f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).Take(rec.Reads.Count - from - 1).All(IsIdle), "no input while holding the lock-up");
            Assert.AreEqual(MoveId.BreakLockUp, h.PairedMoves.Last().Move);

            // After the break the CPU decides again (certain lock-up chance: it locks up again).
            h.TickUntil(() => h.PairedMoves.Count(p => p.Move == MoveId.LockUp) == 2, 5f, Dt);
            Assert.AreEqual(WrestlerId.Cpu, h.PairedMoves.Last().Initiator);
            Assert.AreEqual(OpponentBrain.BrainState.LockUp, brain.State);
        }

        [Test]
        public void LockUpChanceZero_NeverLocksUp_Strikes()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuComboContinueChance = 1f; t.cpuLockUpChance = 0f;
            });
            var (_, rec) = AttachBrain(h);
            h.TickSeconds(4f, Dt);
            Assert.AreEqual(0, h.PairedMoves.Count);
            Assert.IsFalse(rec.Reads.Any(i => i.LockUpGrapple.Pressed));
            Assert.IsTrue(h.Hits.Any(x => x.Attacker == WrestlerId.Cpu));
        }

        [Test]
        public void LockedUpCpu_GivesNoInput_UntilTheBreak()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(11.5f, -12f), configure: t =>
            {
                t.cpuApproachChance = 0f; t.cpuAttackChance = 0f; t.cpuLockUpChance = 0f; t.cpuRecoverCounterChance = 0f;
            });
            var (brain, rec) = AttachBrain(h);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            int from = rec.Reads.Count;

            h.TickUntil(() => !h.Match.LockUps.IsActive, 5f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).All(IsIdle));
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            h.Tick(1, Dt);
            Assert.AreNotEqual(OpponentBrain.BrainState.Stunned, brain.State, "back to Idle / Evaluate after the break");
        }

        [Test]
        public void LockUpChoice_IsRepeatableWithTheSameSeed()
        {
            List<(WrestlerId, MoveId)> Run()
            {
                using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t => t.cpuLockUpChance = 0.5f);
                AttachBrain(h, seed: 11);
                h.TickSeconds(8f, Dt);
                return h.PairedMoves.Select(m => (m.Initiator, m.Move))
                    .Concat(h.Hits.Select(x => (x.Attacker, x.Move))).ToList();
            }
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
