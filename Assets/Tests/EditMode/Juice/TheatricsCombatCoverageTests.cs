using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-046 "Red flash: every event that deals damage (strikes, stomps, bodyslam, truck/pool crash, run collision, any
    /// other damage source) and every knockdown or knockout flashes the receiving wrestler red ... the same for both
    /// wrestlers." Drives a real CombatMatch through each damage source and feeds each tick's CombatEvents into a
    /// TickEvents by the mapping in ARCHITECTURE 4.7 ("Per-tick resolution"), then checks ImpactClassifier gives every
    /// hurt / downed wrestler an impact (the flash, blood and shake all hang off an impact).
    /// Note: the real collector is private to the CombatTheatrics MonoBehaviour, so this rebuilds it from the documented
    /// mapping; it proves the classifier covers the events Combat really raises, not the MonoBehaviour's wiring.
    /// </summary>
    public class TheatricsCombatCoverageTests
    {
        private const float Dt = 0.01f;
        private static readonly Vector2 P = new Vector2(10f, -12f);

        /// <summary>Collects CombatEvents per tick (ARCHITECTURE 4.7 mapping) and resolves impacts after each tick.</summary>
        private sealed class ImpactRecorder
        {
            private readonly MatchHarness h;
            private readonly TickEvents tick = new TickEvents();
            private readonly float[] lastHealth = new float[2];
            private readonly List<Impact> scratch = new List<Impact>();
            public readonly List<Impact> Impacts = new List<Impact>();
            public readonly List<string> Uncovered = new List<string>();

            public ImpactRecorder(MatchHarness harness)
            {
                h = harness;
                lastHealth[0] = h.Player.Health;
                lastHealth[1] = h.Cpu.Health;
                CombatEvents ev = h.Match.Events;
                ev.HitLanded += hit => tick.Hits.Add(hit);
                ev.ObstacleHit += (id, c) => tick.Obstacles.Add(new KeyValuePair<WrestlerId, Vector2>(id, c));
                ev.KnockedDown += (id, d) => tick.KnockedDown[(int)id] = true;
                ev.StateChanged += (id, from, to) =>
                {
                    if (to == CombatState.KnockedOut) tick.KnockedOut[(int)id] = true;
                    if (from == CombatState.Running && (to == CombatState.KnockedDown || to == CombatState.KnockedOut))
                        tick.DownFromRunning[(int)id] = true;
                };
                ev.HealthChanged += (id, hp) =>
                {
                    int i = (int)id;
                    if (hp < lastHealth[i] - 1e-6f) tick.Damaged[i] = true;
                    lastHealth[i] = hp;
                };
                ev.MatchEnded += r => { tick.HasEnded = true; tick.Ended = r; };
            }

            public void Run(float seconds)
            {
                int n = Mathf.CeilToInt(seconds / Dt);
                for (int k = 0; k < n; k++)
                {
                    tick.Clear();
                    h.Tick(1, Dt);
                    if (tick.IsEmpty) continue;
                    ImpactClassifier.Resolve(tick, h.Player.Position, h.Cpu.Position, scratch);
                    for (int i = 0; i < 2; i++)
                    {
                        bool hurt = tick.Damaged[i] || tick.KnockedDown[i] || tick.KnockedOut[i];
                        if (hurt && !scratch.Any(x => (int)x.Receiver == i))
                            Uncovered.Add($"t={h.Match.ElapsedSeconds:0.00} {(WrestlerId)i} damaged={tick.Damaged[i]} " +
                                          $"down={tick.KnockedDown[i]} out={tick.KnockedOut[i]} with no impact");
                    }
                    Impacts.AddRange(scratch);
                }
            }

            public void AssertEveryHurtTickHasAnImpact() =>
                Assert.IsEmpty(Uncovered, "D-046: every damage / knockdown / knockout must flash its receiver");

            public ImpactKind[] KindsOn(WrestlerId id) => Impacts.Where(i => i.Receiver == id).Select(i => i.Kind).ToArray();
        }

        // D-046 Red flash: strikes (combo 1, 2, 3).
        [Test]
        public void Combo_EachHitIsAnImpact_LightLightHeavy()
        {
            using var h = new MatchHarness(P, P + new Vector2(1.6f, 0f));
            var r = new ImpactRecorder(h);
            h.PlayerInput.StrikeOnTicks(200, 0, 15, 60);
            r.Run(1.5f);
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.LightStrike, ImpactKind.LightStrike, ImpactKind.HeavyStrike },
                r.KindsOn(WrestlerId.Cpu), "one impact per hit; the Strike 3 knockdown rides on the heavy hit");
            Assert.IsEmpty(r.KindsOn(WrestlerId.Player));
        }

        // D-046 "the same for both wrestlers": the CPU's combo on the player.
        [Test]
        public void CpuCombo_OnPlayer_SameImpacts()
        {
            using var h = new MatchHarness(P, P + new Vector2(1.6f, 0f));
            h.CpuIdle = false;
            var r = new ImpactRecorder(h);
            h.CpuInput.StrikeOnTicks(200, 0, 15, 60);
            r.Run(1.5f);
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.LightStrike, ImpactKind.LightStrike, ImpactKind.HeavyStrike },
                r.KindsOn(WrestlerId.Player));
        }

        // D-046 Red flash: stomps.
        [Test]
        public void Stomp_IsAStompImpact()
        {
            using var h = new MatchHarness(P, P + new Vector2(2f, 0f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 10f; t.maxKnockdownTime = 10f; t.knockdownSlideDistance = 0f;
            });
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressStrikeOnce();
            r.Run(1.0f); // Strike 1 knocks down (32 -> 29) and the player recovers to Standing
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            h.PlayerInput.PressStrikeOnce();
            r.Run(0.6f);
            Assert.AreEqual(MoveId.Stomp, h.Hits.Last().Move, "precondition: a stomp landed");
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.LightStrike, ImpactKind.Stomp }, r.KindsOn(WrestlerId.Cpu));
        }

        // D-046 Red flash: bodyslam (no-throw fallback fixture; the throw raises the same HitLanded(Bodyslam)), and the
        // knockdown at the end of the slam (no hit that tick) still flashes.
        [Test]
        public void Bodyslam_LandingIsASlam_LaterKnockdownStillFlashes()
        {
            using var h = new MatchHarness(P, P + new Vector2(1.5f, 0f));
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressGrappleOnce();
            r.Run(Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State, "precondition: locked up");
            h.PlayerInput.PressGrappleOnce();
            r.Run(3f);
            Assert.IsTrue(h.Hits.Any(x => x.Move == MoveId.Bodyslam), "precondition: the slam landed");
            r.AssertEveryHurtTickHasAnImpact();
            ImpactKind[] kinds = r.KindsOn(WrestlerId.Cpu);
            Assert.AreEqual(ImpactKind.Slam, kinds[0]);
            Assert.IsTrue(h.KnockDowns.Any(k => k.id == WrestlerId.Cpu), "precondition: knocked down");
        }

        // D-046 Red flash: truck / pool crash (Struck), and a crash that knocks down is one crash impact.
        [TestCase(14f, false)] // runs left into the truck
        [TestCase(16f, true)]  // runs right into the pool (CPU parked at the far left)
        public void RunIntoObstacle_IsACrash(float startX, bool runRight)
        {
            using var h = new MatchHarness(new Vector2(startX, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f));
            var r = new ImpactRecorder(h);
            if (runRight) h.PlayerInput.HeldMove = Vector2.right;
            h.PlayerInput.PressRunOnce();
            r.Run(3f);
            Assert.AreEqual(1, h.ObstacleHits.Count, "precondition: one crash");
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.Crash }, r.KindsOn(WrestlerId.Player));
        }

        [Test]
        public void RunIntoTruck_KnockedDown_IsOneCrash()
        {
            using var h = new MatchHarness(new Vector2(14f, SliceArena.ObstacleLaneY), cpuStart: new Vector2(1f, -13f),
                configure: t => { t.maxHealth = 32f; t.minKnockdownTime = 10f; t.maxKnockdownTime = 10f; });
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressRunOnce();
            r.Run(2f);
            Assert.AreEqual(CombatState.KnockedDown, h.Player.State, "precondition");
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.Crash }, r.KindsOn(WrestlerId.Player));
        }

        // D-046 Red flash: run collision (both go down; with damage 0 and with damage).
        [TestCase(0f)]
        [TestCase(10f)]
        public void RunCollision_BothGetARunCollisionImpact(float damage)
        {
            using var h = new MatchHarness(new Vector2(5f, -12f), new Vector2(12f, -12f), configure: t => t.runCollisionDamage = damage);
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressRunOnce();
            r.Run(1.5f);
            Assert.AreEqual(2, h.KnockDowns.Count, "precondition: both down");
            r.AssertEveryHurtTickHasAnImpact();
            CollectionAssert.AreEqual(new[] { ImpactKind.RunCollision }, r.KindsOn(WrestlerId.Player));
            CollectionAssert.AreEqual(new[] { ImpactKind.RunCollision }, r.KindsOn(WrestlerId.Cpu));
        }

        // D-046 Red flash: "any other damage source" - a whip run into an obstacle (Irish Whip impact damage).
        [Test]
        public void WhipRunIntoObstacle_IsACrash()
        {
            var arena = new ArenaGeometry(SliceArena.Band, new[] { new ArenaRect(2f, -12.5f, 3f, -11.5f) });
            using var h = new MatchHarness(P, P + new Vector2(1.5f, 0f), arena: arena);
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressGrappleOnce();
            r.Run(Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State, "precondition: locked up");
            h.PlayerInput.PressRunOnce();
            r.Run(4f);
            Assert.AreEqual(1, h.ObstacleHits.Count, "precondition: the whipped wrestler hit the obstacle");
            r.AssertEveryHurtTickHasAnImpact();
            Assert.Contains(ImpactKind.Crash, r.KindsOn(WrestlerId.Cpu));
        }

        // D-046 Red flash on the knockout; the KO is marked on exactly one impact (drives the slow-mo / punch-in).
        [Test]
        public void KnockoutBlow_IsOneKnockoutImpact()
        {
            using var h = new MatchHarness(P, P + new Vector2(1.6f, 0f), configure: t => t.strike1Damage = 200f);
            var r = new ImpactRecorder(h);
            h.PlayerInput.PressStrikeOnce();
            r.Run(1f);
            Assert.IsTrue(h.Match.IsOver, "precondition: knocked out");
            r.AssertEveryHurtTickHasAnImpact();
            Impact[] ko = r.Impacts.Where(i => i.Knockout).ToArray();
            Assert.AreEqual(1, ko.Length);
            Assert.AreEqual(WrestlerId.Cpu, ko[0].Receiver);
            Assert.AreEqual(ImpactKind.LightStrike, ko[0].Kind, "the KO rides on the blow that caused it");
        }
    }
}
