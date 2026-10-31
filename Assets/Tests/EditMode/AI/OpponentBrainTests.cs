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
    /// COMBAT_SPEC "CPU AI" (combat-core slice): deterministic FSM producing intents only (ARCHITECTURE 4.1),
    /// decisions on an interval, configurable probabilities, mashing at the CPU mash rate (D-025),
    /// stomping a downed player, Knockout = no input.
    /// </summary>
    public class OpponentBrainTests
    {
        private const float Dt = 0.02f;

        /// <summary>Wraps an input and records every intent it returns.</summary>
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

        private static void Certain(CombatTuning t)
        {
            t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuComboContinueChance = 1f; t.cpuStompChance = 1f;
        }

        [Test]
        public void Unbound_ReadsIdle()
        {
            var tuning = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                var brain = new OpponentBrain(tuning, () => 0f);
                Assert.IsFalse(brain.IsBound);
                WrestlerIntent i = brain.Read();
                Assert.AreEqual(Vector2.zero, i.Move);
                Assert.IsFalse(i.Strike.Pressed || i.RunWhip.Pressed || i.LockUpGrapple.Pressed);
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        [Test]
        public void ApproachesAndStrikesPlayer()
        {
            using var h = new MatchHarness(new Vector2(6f, -13f), new Vector2(20f, -11.5f), configure: Certain);
            var (brain, _) = AttachBrain(h);
            h.TickUntil(() => h.Hits.Any(x => x.Attacker == WrestlerId.Cpu), 10f, Dt);
            Assert.AreEqual(MoveId.Strike1, h.Hits.First(x => x.Attacker == WrestlerId.Cpu).Move);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing, "faces the player");
        }

        [Test]
        public void ApproachesFromTheLeftToo_TurnsToFace()
        {
            using var h = new MatchHarness(new Vector2(18f, -12f), new Vector2(4f, -13f), configure: Certain);
            AttachBrain(h);
            h.TickUntil(() => h.Hits.Any(x => x.Attacker == WrestlerId.Cpu), 10f, Dt);
            Assert.AreEqual(1, h.Cpu.HorizontalFacing);
        }

        [Test]
        public void FullCombo_KnocksPlayerDown()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: Certain);
            AttachBrain(h);
            h.TickUntil(() => h.Player.State == CombatState.KnockedDown, 10f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 },
                h.Hits.Where(x => x.Attacker == WrestlerId.Cpu).Select(x => x.Move).Take(3).ToArray());
        }

        [Test]
        public void StompsDownedPlayer_WhenChanceIsCertain()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                Certain(t); t.minKnockdownTime = 6f; t.maxKnockdownTime = 6f;
            });
            AttachBrain(h);
            h.TickUntil(() => h.Player.State == CombatState.KnockedDown, 10f, Dt);
            h.TickUntil(() => h.Hits.Any(x => x.Move == MoveId.Stomp), 4f, Dt);
            Assert.AreEqual(WrestlerId.Cpu, h.Hits.First(x => x.Move == MoveId.Stomp).Attacker);
        }

        [Test]
        public void NeverStomps_WhenChanceIsZero()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                Certain(t); t.cpuStompChance = 0f; t.minKnockdownTime = 4f; t.maxKnockdownTime = 4f;
            });
            AttachBrain(h);
            h.TickUntil(() => h.Player.State == CombatState.KnockedDown, 10f, Dt);
            h.TickSeconds(3.5f, Dt);
            Assert.IsFalse(h.MovesStarted.Any(m => m.move == MoveId.Stomp));
        }

        // D-025: mashes at the CPU mash rate while Knocked Down; Pressed is true for one Read per press.
        [Test]
        public void Mashes_AtMashRate_OneReadPerPress_ShortensKnockdown()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 3f; t.maxKnockdownTime = 3f; t.cpuMashRate = 3f; t.cpuAttackChance = 0f;
            });
            var (_, rec) = AttachBrain(h);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            int startRead = rec.Reads.Count;
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 4f, Dt);
            var reads = rec.Reads.Skip(startRead).ToList();
            int presses = reads.Count(r => r.Strike.Pressed);
            Assert.AreEqual(t * h.Tuning.cpuMashRate, presses, 1.5f, "mash rate");
            for (int i = 1; i < reads.Count; i++)
                Assert.IsFalse(reads[i].Strike.Pressed && reads[i - 1].Strike.Pressed, "Pressed must not repeat on consecutive reads");
            Assert.Less(t, 3f - 0.3f, "mashing shortened the knockdown");
        }

        [Test]
        public void MashRateZero_NoPresses()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 1f; t.maxKnockdownTime = 1f; t.cpuMashRate = 0f; t.cpuAttackChance = 0f;
            });
            var (_, rec) = AttachBrain(h);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            int startRead = rec.Reads.Count;
            float t = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 3f, Dt);
            Assert.IsFalse(rec.Reads.Skip(startRead).Any(r => r.Strike.Pressed));
            Assert.AreEqual(1f, t, 0.05f);
        }

        // Decisions only on the interval: chosen (non-forced) states change at most once per interval.
        [Test]
        public void ChosenStateChanges_AreSpacedByDecisionInterval()
        {
            using var h = new MatchHarness(new Vector2(6f, -13f), new Vector2(20f, -11.5f), configure: t =>
            {
                t.cpuDecisionInterval = 0.5f; t.cpuApproachChance = 0.5f; t.cpuAttackChance = 0.5f; t.cpuComboContinueChance = 0.5f;
            });
            var (brain, _) = AttachBrain(h, seed: 3);
            var changes = new List<float>();
            OpponentBrain.BrainState last = brain.State;
            h.Tick(400, Dt, _ =>
            {
                if (brain.State != last)
                {
                    bool forced = brain.State == OpponentBrain.BrainState.Stunned || last == OpponentBrain.BrainState.Stunned
                        || brain.State == OpponentBrain.BrainState.Knockout;
                    if (!forced) changes.Add(h.Match.ElapsedSeconds);
                    last = brain.State;
                }
            });
            Assert.That(changes.Count, Is.GreaterThan(1), "the brain should change state during the run");
            for (int i = 1; i < changes.Count; i++)
                Assert.That(changes[i] - changes[i - 1], Is.GreaterThanOrEqualTo(0.5f - Dt - 1e-4f), $"changes at {changes[i - 1]} and {changes[i]}");
        }

        [Test]
        public void SameSeed_SameMatch()
        {
            Vector2[] Run(int seed)
            {
                using var h = new MatchHarness(new Vector2(6f, -13f), new Vector2(20f, -11.5f), configure: t => { t.cpuApproachChance = 0.6f; t.cpuAttackChance = 0.6f; });
                AttachBrain(h, seed);
                var trace = new List<Vector2>();
                h.Tick(300, Dt, _ => trace.Add(h.Cpu.Position));
                return trace.ToArray();
            }
            CollectionAssert.AreEqual(Run(11), Run(11));
        }

        [Test]
        public void Knockout_NoInput()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t => { t.maxHealth = 3f; t.cpuAttackChance = 0f; });
            var (brain, rec) = AttachBrain(h);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f, Dt);
            h.Tick(1, Dt);
            Assert.AreEqual(OpponentBrain.BrainState.Knockout, brain.State);
            WrestlerIntent last = rec.Reads.Last();
            Assert.AreEqual(Vector2.zero, last.Move);
            Assert.IsFalse(last.Strike.Pressed || last.RunWhip.Pressed || last.LockUpGrapple.Pressed);
        }

        [Test]
        public void Recovering_WithZeroCounterChance_StandsStillAndRecovers()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t =>
            {
                Certain(t); t.cpuAttackChance = 0f; t.cpuRecoverCounterChance = 0f; t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.cpuMashRate = 0f;
            });
            var (brain, _) = AttachBrain(h);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.Recovering, 2f, Dt);
            h.Tick(2, Dt);
            Assert.AreEqual(OpponentBrain.BrainState.Recover, brain.State);
            Vector2 p = h.Cpu.Position;
            h.TickUntil(() => h.Cpu.State == CombatState.Standing, 3f, Dt);
            V2Assert.AreEqual(p, h.Cpu.Position, 1e-6f, "stood still to recover");
            Assert.AreEqual(h.Tuning.recoveryCap, h.Cpu.Health, 1e-4f);
        }

        // The brain never touches combat: identical intents from a ScriptedInput replay give the same match.
        [Test]
        public void BrainIntents_ReplayedThroughScriptedInput_GiveSameMatch()
        {
            List<WrestlerIntent> recorded;
            Vector2 cpuEnd; float playerHealth;
            using (var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: Certain))
            {
                var (_, rec) = AttachBrain(h);
                h.Tick(150, Dt);
                recorded = rec.Reads;
                cpuEnd = h.Cpu.Position; playerHealth = h.Player.Health;
            }
            using (var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: Certain))
            {
                h.CpuIdle = false;
                foreach (WrestlerIntent i in recorded) h.CpuInput.Enqueue(i);
                h.Tick(150, Dt);
                V2Assert.AreEqual(cpuEnd, h.Cpu.Position, 1e-6f);
                Assert.AreEqual(playerHealth, h.Player.Health, 1e-6f);
            }
        }
    }
}
