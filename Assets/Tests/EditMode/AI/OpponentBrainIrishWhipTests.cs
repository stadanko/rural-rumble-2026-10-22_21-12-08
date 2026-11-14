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
    /// COMBAT_SPEC CPU AI "Lock Up/Grapple" (lock-up slice step 3): "attempts a standing lock-up and, if successful,
    /// performs a grapple or Irish Whip". Holding its own lock-up, the CPU rolls bodyslam first, then Irish Whip
    /// (cpuWhipChance) on each decision tick, with no direction held (D-039: the runner passes the CPU). Whipped,
    /// it gives no input (D-028 no control) and rides the run out. Same Combat rules as the player.
    /// </summary>
    public class OpponentBrainIrishWhipTests
    {
        private const float Dt = 0.02f;
        private static readonly Vector2 PlayerStart = new Vector2(8f, -12f);
        private static readonly Vector2 CpuStart = new Vector2(14f, -12f);

        private sealed class Recording : IWrestlerInput
        {
            private readonly IWrestlerInput inner;
            private readonly System.Func<float> clock;
            public readonly List<(float time, WrestlerIntent intent)> Reads = new List<(float, WrestlerIntent)>();
            public Recording(IWrestlerInput inner, System.Func<float> clock) { this.inner = inner; this.clock = clock; }
            public WrestlerIntent Read() { WrestlerIntent i = inner.Read(); Reads.Add((clock(), i)); return i; }
        }

        private static (OpponentBrain brain, Recording rec) AttachBrain(MatchHarness h, int seed = 7)
        {
            var brain = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, seed);
            var rec = new Recording(brain, () => h.Match.ElapsedSeconds);
            h.CpuSource = rec;
            return (brain, rec);
        }

        private static bool IsIdle(WrestlerIntent i) =>
            i.Move == Vector2.zero && !i.Strike.Pressed && !i.RunWhip.Pressed && !i.LockUpGrapple.Pressed;

        private static void WhipOnly(CombatTuning t)
        {
            t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f;
            t.cpuBodyslamChance = 0f; t.cpuWhipChance = 1f; t.cpuStompChance = 0f;
        }

        [Test]
        public void WhipChanceCertain_LocksUp_ThenWhips_OneIntervalIn_NoDirection()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: WhipOnly);
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.PairedMoves.Count > 0, 10f, Dt);
            float lockedAt = h.Match.ElapsedSeconds;

            h.TickUntil(() => h.Match.LockUps.CurrentMove == MoveId.IrishWhip, 2f, Dt);
            PairedMoveInfo whip = h.PairedMoves.Last();
            Assert.AreEqual(MoveId.IrishWhip, whip.Move);
            Assert.AreEqual(WrestlerId.Cpu, whip.Initiator);
            Assert.AreEqual(h.Player.HorizontalFacing, whip.WhipDirection, "no direction: the way the player faces");
            Assert.IsTrue(whip.WhipThrough, "past the CPU");
            Assert.AreEqual(CombatState.Grappling, h.Cpu.State);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(OpponentBrain.BrainState.LockUp, brain.State);

            var press = rec.Reads.Last(r => r.intent.RunWhip.Pressed);
            Assert.AreEqual(Vector2.zero, press.intent.Move, "no direction held");
            Assert.That(press.time - lockedAt, Is.GreaterThanOrEqualTo(h.Tuning.cpuDecisionInterval - 1e-4f));
            Assert.That(press.time - lockedAt, Is.LessThanOrEqualTo(h.Tuning.cpuDecisionInterval + 2 * Dt));
        }

        [Test]
        public void DuringItsWhip_CpuGivesNoInput_ThenReturnsToIdleEvaluate()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: WhipOnly);
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.Match.LockUps.CurrentMove == MoveId.IrishWhip, 12f, Dt);
            int from = rec.Reads.Count;

            h.TickUntil(() => !h.Match.LockUps.IsActive, 2f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).Take(rec.Reads.Count - from - 1).All(r => IsIdle(r.intent)),
                "no input while Grappling (whipping)");
            h.Tick(1, Dt);
            Assert.AreNotEqual(OpponentBrain.BrainState.LockUp, brain.State, "back to Idle / Evaluate");
            Assert.AreNotEqual(OpponentBrain.BrainState.Stunned, brain.State);
        }

        [Test]
        public void BodyslamRollFirst_WhipOnlyWhenTheBodyslamRollFails()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: t =>
            {
                WhipOnly(t);
                t.cpuBodyslamChance = 1f;
            });
            AttachBrain(h);
            h.TickUntil(() => h.Hits.Count > 0, 12f, Dt);
            Assert.AreEqual(MoveId.Bodyslam, h.PairedMoves.Last().Move);
            Assert.IsFalse(h.PairedMoves.Any(m => m.Move == MoveId.IrishWhip));
        }

        [Test]
        public void WhippedCpu_GivesNoInput_UntilTheRunEnds()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(11.5f, -12f), configure: t =>
            {
                t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f; t.cpuRecoverCounterChance = 1f;
            });
            var (brain, rec) = AttachBrain(h);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressRunOnce(); // no direction: the CPU is whipped left, past the player
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);
            int from = rec.Reads.Count;

            h.TickUntil(() => h.Cpu.State != CombatState.Running, 4f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).All(r => IsIdle(r.intent)), "D-028: no input while whipped");
            Assert.AreEqual(OpponentBrain.BrainState.Stunned, brain.State);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State, "the run ended at the arena end");

            h.Tick(1, Dt);
            Assert.AreNotEqual(OpponentBrain.BrainState.Stunned, brain.State, "evaluates again once the run is over");
        }

        [Test]
        public void WhipChoice_IsRepeatableWithTheSameSeed()
        {
            List<(WrestlerId, MoveId, int)> Run()
            {
                using var h = new MatchHarness(PlayerStart, CpuStart, configure: t =>
                {
                    t.cpuLockUpChance = 0.6f; t.cpuBodyslamChance = 0.3f; t.cpuWhipChance = 0.5f;
                });
                AttachBrain(h, seed: 5);
                h.TickSeconds(12f, Dt);
                return h.PairedMoves.Select(m => (m.Initiator, m.Move, m.WhipDirection)).ToList();
            }
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
