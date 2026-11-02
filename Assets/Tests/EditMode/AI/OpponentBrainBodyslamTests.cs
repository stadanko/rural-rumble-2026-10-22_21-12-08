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
    /// COMBAT_SPEC CPU AI "Lock Up/Grapple" (lock-up slice step 2): "attempts a standing lock-up and, if
    /// successful, performs a grapple"; demo script step 6 ("Peaux executes a bodyslam, inflicting damage and
    /// putting Barry into a knockdown") and step 7 (back to Idle / Evaluate). The CPU presses Grapple at
    /// cpuBodyslamChance per decision tick while holding its own lock-up, through the same Combat rules.
    /// </summary>
    public class OpponentBrainBodyslamTests
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

        private static void Aggressive(CombatTuning t)
        {
            t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f; t.cpuBodyslamChance = 1f;
            t.cpuStompChance = 0f;
        }

        [Test]
        public void BodyslamChanceCertain_LocksUp_ThenBodyslams_OneIntervalIn()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: Aggressive);
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.PairedMoves.Count > 0, 10f, Dt);
            float lockedAt = h.Match.ElapsedSeconds;

            h.TickUntil(() => h.Cpu.State == CombatState.Grappling, 2f, Dt);
            PairedMoveInfo slam = h.PairedMoves.Last();
            Assert.AreEqual(MoveId.Bodyslam, slam.Move);
            Assert.AreEqual(WrestlerId.Cpu, slam.Initiator);
            Assert.AreEqual(CombatState.Grappled, h.Player.State);
            Assert.AreEqual(OpponentBrain.BrainState.LockUp, brain.State, "Lock Up/Grapple");

            float pressedAt = rec.Reads.Last(r => r.intent.LockUpGrapple.Pressed).time;
            Assert.That(pressedAt - lockedAt, Is.GreaterThanOrEqualTo(h.Tuning.cpuDecisionInterval - 1e-4f),
                "the first bodyslam roll is one decision interval into the lock-up");
            Assert.That(pressedAt - lockedAt, Is.LessThanOrEqualTo(h.Tuning.cpuDecisionInterval + 2 * Dt));

            // Demo step 6: damage and a knockdown.
            h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.bodyslamDamage, h.Player.Health, 1e-4f);
            Assert.AreEqual(MoveId.Bodyslam, h.Hits.Single().Move);
        }

        [Test]
        public void DuringItsBodyslam_CpuGivesNoInput_ThenReturnsToIdleEvaluate()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: Aggressive);
            var (brain, rec) = AttachBrain(h);
            h.TickUntil(() => h.Cpu.State == CombatState.Grappling, 12f, Dt);
            int from = rec.Reads.Count;

            h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).Take(rec.Reads.Count - from - 1).All(r => IsIdle(r.intent)),
                "no input while Grappling");

            h.Tick(1, Dt);
            Assert.AreNotEqual(OpponentBrain.BrainState.LockUp, brain.State, "demo step 7: back to Idle / Evaluate");
            Assert.AreNotEqual(OpponentBrain.BrainState.Stunned, brain.State);
        }

        [Test]
        public void BodyslamChanceZero_HoldsTheLockUpUntilTheBreak()
        {
            using var h = new MatchHarness(PlayerStart, CpuStart, configure: t => { Aggressive(t); t.cpuBodyslamChance = 0f; });
            var (_, rec) = AttachBrain(h);
            h.TickUntil(() => h.PairedMoves.Count > 0, 10f, Dt);
            int from = rec.Reads.Count;
            h.TickUntil(() => h.PairedMoves.Count == 2, 3f, Dt);
            Assert.AreEqual(MoveId.BreakLockUp, h.PairedMoves[1].Move);
            Assert.IsFalse(rec.Reads.Skip(from).Any(r => r.intent.LockUpGrapple.Pressed));
            Assert.AreEqual(0, h.Hits.Count);
        }

        [Test]
        public void GrappledCpu_GivesNoInput_ThenIsKnockedDownAndMashes()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(11.5f, -12f), configure: t =>
            {
                t.cpuApproachChance = 0f; t.cpuAttackChance = 0f; t.cpuLockUpChance = 0f; t.cpuRecoverCounterChance = 0f;
            });
            var (brain, rec) = AttachBrain(h);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            int from = rec.Reads.Count;

            h.TickUntil(() => !h.Match.LockUps.IsActive, 4f, Dt);
            Assert.IsTrue(rec.Reads.Skip(from).All(r => IsIdle(r.intent)), "Wrestler B has no control");
            Assert.AreEqual(OpponentBrain.BrainState.Stunned, brain.State);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);

            int mashFrom = rec.Reads.Count;
            h.TickSeconds(0.8f, Dt);
            Assert.IsTrue(rec.Reads.Skip(mashFrom).Any(r => r.intent.Strike.Pressed), "mashes once Knocked Down (D-025)");
        }

        [Test]
        public void BodyslamChoice_IsRepeatableWithTheSameSeed()
        {
            List<(WrestlerId, MoveId, float)> Run()
            {
                using var h = new MatchHarness(PlayerStart, CpuStart, configure: t =>
                {
                    t.cpuLockUpChance = 0.6f; t.cpuBodyslamChance = 0.3f;
                });
                AttachBrain(h, seed: 5);
                h.TickSeconds(10f, Dt);
                return h.PairedMoves.Select(m => (m.Initiator, m.Move, 0f))
                    .Concat(h.Hits.Select(x => (x.Attacker, x.Move, x.Damage))).ToList();
            }
            CollectionAssert.AreEqual(Run(), Run());
        }
    }
}
