using System.Linq;
using NUnit.Framework;
using RuralRumble.AI;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-046 knockback: the short pushback on a hit that does not knock down (Strike 1 / 2 -> Struck: hitPushbackDistance
    /// over pushbackDuration on x, away from the attacker, normal movement clamping, cut short so the 3-hit combo still
    /// connects), the truck/pool crash pushback (crashPushbackDistance, away from the obstacle), and the D-046 defaults
    /// (knockdown slide 1.0 over 0.45 s). The knockdown slide itself is covered by KnockdownFallTests.
    /// </summary>
    public class PushbackTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private const float Dt = 0.01f;

        private static Vector2 Cpu(float gap) => P + new Vector2(gap, 0f);

        [Test]
        public void Defaults_D046()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                Assert.AreEqual(1.0f, t.knockdownSlideDistance, 1e-6f, "knockdown slide 2x D-041's 0.5");
                Assert.AreEqual(0.45f, t.knockdownSlideDuration, 1e-6f);
                Assert.AreEqual(0.2f, t.hitPushbackDistance, 1e-6f);
                Assert.AreEqual(0.4f, t.crashPushbackDistance, 1e-6f, "2x the hit pushback");
                Assert.AreEqual(0.1f, t.pushbackDuration, 1e-6f);
            }
            finally { Object.DestroyImmediate(t); }
        }

        // ================================================================ Hit pushback

        [Test]
        public void Strike1_PushesStruckWrestlerAway_LinearOverDuration_DepthUnchanged()
        {
            using var h = new MatchHarness(P, Cpu(1.4f)); // 1.4 + 0.2 stays well inside the kick's 2.3: no cap
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Assert.AreEqual(1, h.CpuW.PushbackSide, "away from the attacker (+x)");
            Vector2 start = h.Cpu.Position;
            Vector2 attacker = h.Player.Position;
            float d = h.Tuning.hitPushbackDistance, T = h.Tuning.pushbackDuration;

            h.Tick(5, Dt); // 0.05 s of 0.1 s
            Assert.AreEqual(start.x + d * (0.05f / T), h.Cpu.Position.x, 1e-4f, "linear");

            h.TickSeconds(T, Dt);
            Assert.AreEqual(start.x + d, h.Cpu.Position.x, 1e-4f);
            Assert.AreEqual(start.y, h.Cpu.Position.y, 1e-6f);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State, "still Struck: the pushback is shorter than Struck");

            h.TickSeconds(1f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(start.x + d, h.Cpu.Position.x, 1e-4f, "no further movement after the pushback");
            Assert.AreEqual(0, h.CpuW.PushbackSide);
            V2Assert.AreEqual(attacker, h.Player.Position, 1e-6f, "the attacker is rooted (D-024), not pulled along");
        }

        [Test]
        public void Strike1_FromTheRight_PushesTowardMinusX()
        {
            using var h = new MatchHarness(P, P - new Vector2(1.4f, 0f));
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            Assert.AreEqual(-1, h.CpuW.PushbackSide);
            float x = h.Cpu.Position.x;
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(x - h.Tuning.hitPushbackDistance, h.Cpu.Position.x, 1e-4f);
        }

        [Test]
        public void ComboHit2_RestartsThePushback()
        {
            using var h = new MatchHarness(P, Cpu(1.2f)); // 1.2 + 2 x 0.2 = 1.6: both pushbacks in full
            float x0 = h.Cpu.Position.x;
            h.PlayerInput.StrikeOnTicks(200, 0, 15);
            h.TickUntil(() => h.Hits.Count >= 2, 2f, Dt);
            Assert.AreEqual(MoveId.Strike2, h.Hits[1].Move);
            Assert.AreEqual(x0 + h.Tuning.hitPushbackDistance, h.Cpu.Position.x, 1e-4f, "hit 1's pushback was done");
            h.TickSeconds(0.2f, Dt);
            Assert.AreEqual(x0 + 2f * h.Tuning.hitPushbackDistance, h.Cpu.Position.x, 1e-4f);
        }

        // Hard requirement (D-046): a combo that connected without pushback still connects with it.
        [TestCase(1.0f)]  // feet boxes touching
        [TestCase(1.6f)]  // the CPU's strike distance
        [TestCase(2.0f)]  // StrikeTests' standard start
        [TestCase(2.2f)]
        [TestCase(2.29f)] // just inside the kick's reach (strike3Reach 1.6 + hurtHalfWidth 0.7)
        public void Combo_StillConnectsAllThreeHits(float gap)
        {
            using var h = new MatchHarness(P, Cpu(gap));
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            h.TickSeconds(1.5f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 }, h.Hits.Select(x => x.Move).ToArray());
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(0, h.CpuW.PushbackSide, "a knockdown has the slide, not a pushback");
            Assert.AreEqual(1, h.Cpu.FallSide);
        }

        [Test]
        public void Combo_PushbackIsCutShortAtTheKickReach()
        {
            using var h = new MatchHarness(P, Cpu(2.2f));
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            h.TickSeconds(0.2f, Dt);
            float kickRange = h.Tuning.strike3Reach + h.Tuning.hurtHalfWidth;
            float gap = h.Cpu.Position.x - h.Player.Position.x;
            Assert.That(gap, Is.InRange(kickRange - 0.01f, kickRange), "pushed 0.1, not the full 0.2");
        }

        [Test]
        public void Pushback_TuningEdit_AppliesToTheNextHit()
        {
            using var h = new MatchHarness(P, Cpu(1.2f));
            h.Tuning.hitPushbackDistance = 0.5f;
            h.Tuning.pushbackDuration = 0.05f;
            float x = h.Cpu.Position.x;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            h.TickSeconds(0.1f, Dt);
            Assert.AreEqual(x + 0.5f, h.Cpu.Position.x, 1e-4f);
        }

        [Test]
        public void Pushback_Zero_DoesNotMove()
        {
            using var h = new MatchHarness(P, Cpu(1.4f), configure: t => t.hitPushbackDistance = 0f);
            float x = h.Cpu.Position.x;
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            h.TickSeconds(0.5f, Dt);
            Assert.AreEqual(x, h.Cpu.Position.x, 1e-6f);
        }

        [Test]
        public void Pushback_StopsAtArenaEnd()
        {
            float edge = SliceArena.BandXMax - 0.5f; // feetHalfWidth 0.5
            using var h = new MatchHarness(new Vector2(edge - 1.4f, -12f), new Vector2(edge - 0.1f, -12f));
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            h.TickSeconds(0.3f, Dt);
            Assert.AreEqual(edge, h.Cpu.Position.x, 1e-4f, "clamped at the arena end like any movement");
        }

        // ================================================================ Crash pushback

        [Test]
        public void TruckCrash_NotKnockedDown_PushesAwayFromTheTruck()
        {
            using var h = new MatchHarness(new Vector2(14f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f)); // runs left
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Player.State == CombatState.Struck, 2f, Dt);
            Assert.AreEqual(1, h.PlayerW.PushbackSide, "away from the truck (+x)");
            float face = SliceArena.Truck.XMax + h.Tuning.feetHalfWidth;
            h.TickSeconds(0.3f, Dt, afterEachTick: _ => h.AssertPlayerOutsideObstacles("crash pushback"));
            Assert.AreEqual(face + h.Tuning.crashPushbackDistance, h.Player.Position.x, 0.01f);
            Assert.AreEqual(0, h.Player.FallSide);
        }

        [Test]
        public void TruckCrash_KnockedDown_SlidesInsteadOfPushback()
        {
            using var h = new MatchHarness(new Vector2(14f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f),
                configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 10f; t.maxKnockdownTime = 10f; });
            h.PlayerInput.PressRunOnce();
            h.TickUntil(() => h.Player.State == CombatState.KnockedDown, 2f, Dt);
            Assert.AreEqual(0, h.PlayerW.PushbackSide);
            float x = h.Player.Position.x;
            h.TickSeconds(1f, Dt);
            Assert.AreEqual(x + h.Tuning.knockdownSlideDistance, h.Player.Position.x, 1e-4f, "the full D-046 slide (1.0)");
        }

        // ================================================================ CPU

        [Test]
        public void CpuCombo_StillKnocksThePlayerDown_WithPushback()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
            {
                t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuComboContinueChance = 1f;
            });
            h.CpuSource = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, 7);
            h.TickUntil(() => h.Hits.Count > 0, 10f, Dt);
            float x = h.Player.Position.x;
            h.TickUntil(() => h.Player.State == CombatState.KnockedDown, 3f, Dt);
            CollectionAssert.AreEqual(new[] { MoveId.Strike1, MoveId.Strike2, MoveId.Strike3 },
                h.Hits.Where(e => e.Attacker == WrestlerId.Cpu).Select(e => e.Move).Take(3).ToArray());
            Assert.Less(h.Player.Position.x, x, "the player was pushed back (away from the CPU on his right)");
        }
    }
}
