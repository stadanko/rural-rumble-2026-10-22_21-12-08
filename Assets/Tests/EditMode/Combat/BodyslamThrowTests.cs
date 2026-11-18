using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuralRumble.AI;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-044 bodyslam throw (ACCEPTANCE_TESTS T1.1-T1.15; COMBAT_SPEC Lock Up/Grappling "Bodyslam throw", Tuning Values;
    /// ARCHITECTURE 6 "Bodyslam throw (D-044): integration contract"). Barry slams Peaux throws Peaux past Barry along x at
    /// the lock-up depth. The fixture enables Barry's throw and leaves Peaux's entry disabled (fixture-only since D-045,
    /// which turned Peaux's throw on in the project asset), so Peaux slams Barry here is the step 2 no-throw fallback;
    /// every assertion reads the values back from the fixture. T1.14 checks the project asset.
    /// Default layout: Player = Barry at (10, -12) facing right, CPU = Peaux 1.5 to the right (in lock-up reach), so the
    /// throw goes left (T = -1).
    /// </summary>
    public class BodyslamThrowTests
    {
        private const float Dt = 0.01f;
        private const float Ppu = 60f;
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(11.5f, -12f);

        private WrestlerDefinition barry;
        private WrestlerDefinition peaux;

        [SetUp]
        public void SetUp()
        {
            barry = ScriptableObject.CreateInstance<WrestlerDefinition>();
            peaux = ScriptableObject.CreateInstance<WrestlerDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(barry);
            Object.DestroyImmediate(peaux);
        }

        /// <summary>Fixture values (not the project asset's): Barry throws (0.42 / 208 / 412 / 0.10, landing 0.62); Peaux's entry is disabled (0.475), covering the no-throw fallback.</summary>
        private void ConfigureThrow(CombatTuning t)
        {
            t.bodyslamStartupByInitiator = new[] { new WrestlerStartup(barry, 0.62f), new WrestlerStartup(peaux, 0.475f) };
            t.bodyslamThrowBySlammer = new[]
            {
                new BodyslamThrow(barry, true, 0.42f, 208f, 412f, 0.10f),
                new BodyslamThrow(peaux, false, 0.42f, 208f, 412f, 0.10f),
            };
        }

        private MatchHarness BarrySlamsPeaux(Vector2? p = null, Vector2? c = null, Func<float> random01 = null,
            Action<CombatTuning> extra = null, ArenaGeometry arena = null) =>
            new MatchHarness(p ?? P, c ?? C, arena, t => { ConfigureThrow(t); extra?.Invoke(t); },
                random01 ?? (() => 0.5f), barry, peaux);

        /// <summary>Player locks up the CPU, then presses Grapple on the next tick (the press tick).</summary>
        private static void LockUpAndGrapple(MatchHarness h)
        {
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State, "setup: locked up");
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.Bodyslam, h.Match.LockUps.CurrentMove, "setup: bodyslam started");
        }

        private static BodyslamThrow Entry(MatchHarness h, WrestlerDefinition slammer)
        {
            Assert.IsTrue(h.Tuning.TryGetBodyslamThrow(slammer, out BodyslamThrow e));
            return e;
        }

        private static float Landing(MatchHarness h, WrestlerDefinition slammer) => h.Tuning.BodyslamTimingFor(slammer).Startup;

        private static float DrawnPx(BodyslamThrow e, float r) => e.landingDistancePx * (1f + e.distanceVariance * (2f * r - 1f));

        /// <summary>Ticks until the receiver leaves Grappled (the landing). Returns seconds since the press.</summary>
        private static float TickToLanding(MatchHarness h, float max = 2f) =>
            h.TickUntil(() => h.Cpu.State != CombatState.Grappled, max, Dt);

        // ================================================================ T1.1 rollout per slammer

        [Test]
        public void EnabledSlammer_Throws()
        {
            using var h = BarrySlamsPeaux();
            float cAt = 0f;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            cAt = h.Cpu.Position.x;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(-1, h.PairedMoves.Last().ThrowDirection);
            Assert.AreEqual(-1, h.Match.LockUps.BodyslamThrowDirection);
            TickToLanding(h);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.That(h.Cpu.Position.x, Is.LessThan(h.Player.Position.x), "landed past the slammer");
            Assert.AreNotEqual(cAt, h.Cpu.Position.x);
        }

        [Test]
        public void DisabledOrMissingEntry_OldBodyslamUnchanged()
        {
            // Disabled entry: Player = Peaux slams CPU = Barry.
            using (var h = new MatchHarness(P, C, configure: ConfigureThrow, playerWrestler: peaux, cpuWrestler: barry))
                AssertOldBodyslam(h, 0.475f);

            // No entry for the slammer: Barry slams with an empty throw list.
            using (var h = new MatchHarness(P, C, configure: t => { ConfigureThrow(t); t.bodyslamThrowBySlammer = new BodyslamThrow[0]; },
                       playerWrestler: barry, cpuWrestler: peaux))
                AssertOldBodyslam(h, 0.62f);
        }

        [Test]
        public void NoDefinitions_NoThrow()
        {
            using var h = new MatchHarness(P, C, configure: ConfigureThrow);
            AssertOldBodyslam(h, h.Tuning.bodyslamTiming.Startup);
        }

        /// <summary>The step 2 bodyslam: ThrowDirection 0, Timing = bodyslamTiming with the slammer's Startup, roots never
        /// move, damage at Startup, B Knocked Down after Active + Recovery.</summary>
        private static void AssertOldBodyslam(MatchHarness h, float startup)
        {
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 pAt = h.Player.Position, cAt = h.Cpu.Position;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            PairedMoveInfo info = h.PairedMoves.Last();
            Assert.AreEqual(MoveId.Bodyslam, info.Move);
            Assert.AreEqual(0, info.ThrowDirection);
            Assert.AreEqual(0, h.Match.LockUps.BodyslamThrowDirection);
            Assert.AreEqual(startup, info.Timing.Startup, 1e-6f);
            Assert.AreEqual(h.Tuning.bodyslamTiming.Active, info.Timing.Active, 1e-6f);
            Assert.AreEqual(h.Tuning.bodyslamTiming.Recovery, info.Timing.Recovery, 1e-6f);

            float t = 0f;
            while (h.Match.LockUps.IsActive && t < 5f)
            {
                h.Tick(1, Dt);
                t += Dt;
                V2Assert.AreEqual(pAt, h.Player.Position, 1e-6f, "slammer root");
                V2Assert.AreEqual(cAt, h.Cpu.Position, 1e-6f, "receiver root");
                if (h.Hits.Count == 0) Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
            }
            Assert.AreEqual(startup + h.Tuning.bodyslamTiming.Active + h.Tuning.bodyslamTiming.Recovery, t, 2 * Dt,
                "Knocked Down after Active + Recovery");
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(h.Tuning.bodyslamDamage, h.Hits.Single().Damage, 1e-5f);
            Assert.AreEqual(0, h.ObstacleHits.Count);
        }

        // ================================================================ T1.2 PairedMoveInfo

        [Test]
        public void PairedMoveInfo_ThrowDirectionIsReceiversFacing_BothSides()
        {
            foreach (bool rightSide in new[] { false, true })
            {
                // Slammer facing right (T = -1) or, mirrored, facing left (T = +1).
                Vector2 p = rightSide ? new Vector2(16f, -12f) : P;
                Vector2 c = rightSide ? new Vector2(14.5f, -12f) : C;
                using var h = BarrySlamsPeaux(p, c);
                LockUpAndGrapple(h);
                PairedMoveInfo info = h.PairedMoves.Last();
                int facing = rightSide ? -1 : 1;
                Assert.AreEqual(facing, info.Facing, "slammer's lock-up facing");
                Assert.AreEqual(-facing, info.ThrowDirection, "T = -Facing");
                Assert.AreEqual(-facing, h.Cpu.HorizontalFacing, "= B's lock-up facing");
                Assert.AreEqual((WrestlerId.Player, WrestlerId.Cpu), (info.Initiator, info.Receiver));
                Assert.AreEqual(Landing(h, barry), info.Timing.Startup, 1e-6f, "Startup = the slammer's landing");
                Assert.AreEqual(0f, info.Timing.Active, 0f);
                Assert.AreEqual(0f, info.Timing.Recovery, 0f);
                Assert.AreEqual(CombatState.Grappling, h.Player.State);
                Assert.AreEqual(CombatState.Grappled, h.Cpu.State);
                Assert.AreEqual(MoveId.Bodyslam, h.Player.CurrentMove);
                Assert.AreEqual(MoveId.Bodyslam, h.Cpu.CurrentMove);
                Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.LockUp, MovePhase.None)));
                Assert.IsFalse(h.MovesStarted.Any(m => m.move == MoveId.Bodyslam));
            }
        }

        // ================================================================ T1.3 / T1.4 lift and flight

        [Test]
        public void BeforeRelease_RootsDoNotMove()
        {
            using var h = BarrySlamsPeaux();
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Vector2 pAt = h.Player.Position, cAt = h.Cpu.Position;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            V2Assert.AreEqual(pAt, h.Player.Position, 0f, "press tick: slammer");
            V2Assert.AreEqual(cAt, h.Cpu.Position, 0f, "press tick: receiver");
            float release = Entry(h, barry).releaseTime;
            for (float t = Dt; t < release - Dt * 0.5f; t += Dt)
            {
                h.Tick(1, Dt);
                V2Assert.AreEqual(pAt, h.Player.Position, 0f, $"t {t:F2}: slammer");
                V2Assert.AreEqual(cAt, h.Cpu.Position, 0f, $"t {t:F2}: receiver");
            }
        }

        [Test]
        public void Flight_IsLinearOnX_AtLockUpDepth_ThroughTheSlammer()
        {
            foreach (bool rightSide in new[] { false, true })
            {
                Vector2 p = rightSide ? new Vector2(16f, -12f) : P;
                Vector2 c = rightSide ? new Vector2(14.5f, -12f) : C;
                using var h = BarrySlamsPeaux(p, c, () => 0.5f);
                LockUpAndGrapple(h);
                Vector2 aAt = h.Player.Position;
                float depth = h.Cpu.Position.y;
                Assert.AreEqual(aAt.y, depth, 1e-6f, "lock-up depth = A's");
                int T = h.PairedMoves.Last().ThrowDirection;
                BodyslamThrow e = Entry(h, barry);
                float release = e.releaseTime, landing = Landing(h, barry);
                float d = DrawnPx(e, 0.5f);
                float step = (d - e.releaseOffsetPx) / Ppu / (landing - release) * Dt; // one tick's step
                bool passed = false;

                float t = 0f;
                while (h.Cpu.State == CombatState.Grappled)
                {
                    h.Tick(1, Dt);
                    t += Dt;
                    V2Assert.AreEqual(aAt, h.Player.Position, 0f, $"t {t:F2}: the slammer never moves");
                    Assert.AreEqual(depth, h.Cpu.Position.y, 0f, $"t {t:F2}: B stays at the lock-up depth");
                    if (h.Cpu.State != CombatState.Grappled) break; // landing tick, checked elsewhere
                    if (t < release) continue; // same float sum as the controller's elapsed time
                    float f = Mathf.Clamp01((t - release) / (landing - release));
                    float expected = aAt.x + T * Mathf.Lerp(e.releaseOffsetPx, d, f) / Ppu;
                    Assert.AreEqual(expected, h.Cpu.Position.x, step + 1e-4f, $"t {t:F2}: linear path");
                    passed |= (h.Cpu.Position.x - aAt.x) * T > 0f;
                    Assert.IsTrue(h.Match.LockUps.IsActive);
                }
                Assert.IsTrue(passed, "B is past A during the flight");
                Assert.AreEqual(0, h.ObstacleHits.Count);
                Assert.AreEqual(0, h.KnockDowns.Count(k => k.id == WrestlerId.Player), "no run collision for A");
                Assert.AreEqual(CombatState.Standing, h.Player.State);
                Assert.AreEqual(aAt.x + T * d / Ppu, h.Cpu.Position.x, 1e-3f, "landed at the drawn distance");
            }
        }

        // ================================================================ T1.5 distance and random draw

        [Test]
        public void LandingDistance_FollowsTheInjectedRandom()
        {
            foreach (float r in new[] { 0f, 0.5f, 0.9999f })
            {
                using var h = BarrySlamsPeaux(random01: () => r);
                LockUpAndGrapple(h);
                float aX = h.Player.Position.x;
                TickToLanding(h);
                BodyslamThrow e = Entry(h, barry);
                float expected = e.landingDistancePx * (1f + e.distanceVariance * (2f * r - 1f)) / Ppu;
                Assert.AreEqual(expected, aX - h.Cpu.Position.x, 1e-3f, $"r {r}: root-to-root distance");
                Assert.AreEqual(expected, h.Match.LockUps.BodyslamThrowDistance, 1e-4f, $"r {r}");
                Assert.IsFalse(h.Match.LockUps.BodyslamThrowBlocked);
            }
            // -10% / median / +10% of 412 px.
            Assert.AreEqual(370.8f, DrawnPx(new BodyslamThrow(null, true, 0.42f, 208f, 412f, 0.10f), 0f), 1e-3f);
            Assert.AreEqual(453.2f, DrawnPx(new BodyslamThrow(null, true, 0.42f, 208f, 412f, 0.10f), 1f), 1e-3f);
        }

        [Test]
        public void RandomDrawnOncePerThrow_NeverWithoutThrow()
        {
            int calls = 0;
            Func<float> counted = () => { calls++; return 0.5f; };

            using (var h = BarrySlamsPeaux(random01: counted))
            {
                h.PlayerInput.PressGrappleOnce();
                h.Tick(1, Dt);
                int afterLockUp = calls;
                h.PlayerInput.PressGrappleOnce();
                h.Tick(1, Dt);
                Assert.AreEqual(afterLockUp + 1, calls, "one draw at the press");
                TickToLanding(h);
                h.TickSeconds(0.5f, Dt);
                Assert.AreEqual(afterLockUp + 1, calls, "no other draw during or after the throw");
            }

            calls = 0;
            using (var h = new MatchHarness(P, C, configure: ConfigureThrow, random01: counted, playerWrestler: peaux, cpuWrestler: barry))
            {
                h.PlayerInput.PressGrappleOnce();
                h.Tick(1, Dt);
                int afterLockUp = calls;
                h.PlayerInput.PressGrappleOnce();
                h.Tick(1, Dt);
                h.TickUntil(() => !h.Match.LockUps.IsActive, 3f, Dt);
                Assert.AreEqual(afterLockUp, calls, "a slam without a throw draws nothing");
            }
        }

        // ================================================================ T1.6 / T1.7 landing

        [Test]
        public void Landing_DamageKnockdownAndSlammerFree_SameTick()
        {
            using var h = BarrySlamsPeaux();
            LockUpAndGrapple(h);
            int T = h.PairedMoves.Last().ThrowDirection;
            int phasesBefore = h.PhaseChanges.Count;

            float t = TickToLanding(h);
            Assert.AreEqual(Landing(h, barry), t, 2 * Dt, "landing at the impact time");

            HitInfo hit = h.Hits.Single();
            Assert.AreEqual((WrestlerId.Player, WrestlerId.Cpu, MoveId.Bodyslam), (hit.Attacker, hit.Defender, hit.Move));
            Assert.AreEqual(h.Tuning.bodyslamDamage, hit.Damage, 1e-5f);
            V2Assert.AreEqual(h.Cpu.Position, hit.ContactPoint, 1e-6f, "contact = B's feet");
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.bodyslamDamage, h.Cpu.Health, 1e-4f);
            Assert.IsTrue(h.HealthChanges.Contains((WrestlerId.Cpu, h.Cpu.Health)));

            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            Assert.AreEqual(0, h.Cpu.FallSide, "no fall");
            float lost = (h.Tuning.maxHealth - h.Cpu.Health) / h.Tuning.maxHealth;
            Assert.AreEqual(Mathf.Lerp(h.Tuning.minKnockdownTime, h.Tuning.maxKnockdownTime, lost),
                h.KnockDowns.Single(k => k.id == WrestlerId.Cpu).duration, 1e-4f, "health-based length");

            Assert.AreEqual(CombatState.Standing, h.Player.State, "slammer free on the same tick");
            Assert.AreEqual(T, h.Player.HorizontalFacing, "slammer faces the throw direction");
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.None, h.Cpu.CurrentMove);
            Assert.IsFalse(h.Match.LockUps.IsActive);
            Assert.AreEqual(0, h.Match.LockUps.BodyslamThrowDirection);

            foreach (WrestlerId id in new[] { WrestlerId.Player, WrestlerId.Cpu })
            {
                var phases = h.PhaseChanges.Skip(phasesBefore).Where(c => c.id == id && c.move == MoveId.Bodyslam)
                    .Select(c => c.phase).ToArray();
                CollectionAssert.AreEqual(new[] { MovePhase.Active, MovePhase.Recovery, MovePhase.None }, phases, id.ToString());
            }

            // No slide while down; then the D-043 get-up and back to Standing.
            Vector2 landed = h.Cpu.Position;
            h.TickUntil(() => h.Cpu.IsGettingUp, 4f, Dt);
            V2Assert.AreEqual(landed, h.Cpu.Position, 0f, "no knockback slide");
            float getUp = h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 1f, Dt);
            Assert.AreEqual(h.Tuning.getUpTime, getUp, 2 * Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }

        [Test]
        public void Landing_LowHealthSlammer_Recovering()
        {
            // Every wrestler counts as below the threshold, so the slammer's base state after the move is Recovering.
            using var h = BarrySlamsPeaux(extra: t => t.recoveringThreshold = t.maxHealth + 0.5f);
            LockUpAndGrapple(h);
            TickToLanding(h);
            Assert.AreEqual(CombatState.Recovering, h.Player.State);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
        }

        [Test]
        public void AfterLanding_SlammerHasControl()
        {
            // Walk.
            using (var h = BarrySlamsPeaux())
            {
                LockUpAndGrapple(h);
                TickToLanding(h);
                Vector2 at = h.Player.Position;
                h.PlayerInput.HeldMove = Vector2.right;
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.Walking, h.Player.State);
                Assert.That(h.Player.Position.x, Is.GreaterThan(at.x));
            }
            // Strike out of stomp range: a combo strike.
            using (var h = BarrySlamsPeaux())
            {
                LockUpAndGrapple(h);
                TickToLanding(h);
                Assert.That(Mathf.Abs(h.Cpu.Position.x - h.Player.Position.x), Is.GreaterThan(h.Tuning.stompRangeX));
                h.PlayerInput.PressStrikeOnce();
                h.Tick(1, Dt);
                Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
            }
            // A short throw that lands inside stomp range: Strike is a Stomp (D-025).
            using (var h = BarrySlamsPeaux(extra: t =>
                   {
                       t.bodyslamThrowBySlammer[0].landingDistancePx = 90f;
                       t.bodyslamThrowBySlammer[0].releaseOffsetPx = 60f;
                   }))
            {
                LockUpAndGrapple(h);
                TickToLanding(h);
                Assert.That(Mathf.Abs(h.Cpu.Position.x - h.Player.Position.x), Is.LessThanOrEqualTo(h.Tuning.stompRangeX));
                h.PlayerInput.PressStrikeOnce();
                h.Tick(1, Dt);
                Assert.AreEqual(MoveId.Stomp, h.Player.CurrentMove);
            }
        }

        // ================================================================ T1.8 / T1.9 blocked

        [Test]
        public void BlockedByArenaEnd_StopsShort_NoExtraDamage()
        {
            // Slammer 5 units from the left end, throwing left: the drawn spot is past the end.
            using var h = BarrySlamsPeaux(new Vector2(5f, -12f), new Vector2(6.5f, -12f));
            LockUpAndGrapple(h);
            TickToLanding(h);
            float minX = SliceArena.BandXMin + h.Tuning.feetHalfWidth;
            Assert.AreEqual(minX, h.Cpu.Position.x, 1e-4f, "stopped at the arena end");
            Assert.IsTrue(h.Match.LockUps.BodyslamThrowBlocked);
            Assert.IsFalse(h.Match.LockUps.BodyslamThrowHitObstacle);
            Assert.AreEqual(0, h.ObstacleHits.Count);
            Assert.AreEqual(h.Tuning.bodyslamDamage, h.Hits.Single().Damage, 1e-5f);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
        }

        [Test]
        public void BlockedByTruck_StopsShort_AddsObstacleDamage()
        {
            // At the obstacle lane, throwing left toward the truck.
            float y = SliceArena.ObstacleLaneY;
            using var h = BarrySlamsPeaux(new Vector2(12f, y), new Vector2(13.5f, y));
            LockUpAndGrapple(h);
            AssertBlockedByObstacle(h, SliceArena.Truck.XMax + h.Tuning.feetHalfWidth);
        }

        [Test]
        public void BlockedByPool_StopsShort_AddsObstacleDamage()
        {
            // At the obstacle lane, slammer facing left, throwing right toward the pool.
            float y = SliceArena.ObstacleLaneY;
            using var h = BarrySlamsPeaux(new Vector2(15f, y), new Vector2(13.5f, y));
            LockUpAndGrapple(h);
            Assert.AreEqual(1, h.PairedMoves.Last().ThrowDirection);
            AssertBlockedByObstacle(h, SliceArena.Pool.XMin - h.Tuning.feetHalfWidth);
        }

        private static void AssertBlockedByObstacle(MatchHarness h, float contactX)
        {
            int obstacleTicks = 0;
            float t = 0f, contactAt = -1f;
            while (h.Cpu.State == CombatState.Grappled && t < 2f)
            {
                int before = h.ObstacleHits.Count;
                h.Tick(1, Dt);
                t += Dt;
                if (h.ObstacleHits.Count > before)
                {
                    obstacleTicks++;
                    contactAt = t;
                    Assert.AreEqual(0, h.Hits.Count, "no damage at the contact tick");
                    Assert.AreEqual(CombatState.Grappled, h.Cpu.State, "the flight ends at the landing time, not at contact");
                }
            }
            Assert.AreEqual(1, obstacleTicks, "ObstacleHit once");
            Assert.AreEqual(1, h.ObstacleHits.Count);
            Assert.AreEqual(WrestlerId.Cpu, h.ObstacleHits[0].id);
            Assert.That(contactAt, Is.LessThan(t), "contact before the landing");
            Assert.AreEqual(Landing(h, (h.PlayerW).Definition), t, 2 * Dt, "lands at the landing time");
            Assert.AreEqual(contactX, h.Cpu.Position.x, 0.01f, "stopped at contact");
            Assert.IsTrue(h.Match.LockUps.BodyslamThrowBlocked);
            Assert.IsTrue(h.Match.LockUps.BodyslamThrowHitObstacle);
            Assert.AreEqual(h.Tuning.bodyslamDamage + h.Tuning.obstacleCollisionDamage, h.Hits.Single().Damage, 1e-5f,
                "both damages in one HitLanded");
            Assert.AreEqual(MoveId.Bodyslam, h.Hits.Single().Move);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            foreach (ArenaRect o in h.Match.Arena.Obstacles)
                Assert.IsFalse(SliceArena.FeetInsideObstacle(h.Cpu.Position, h.HalfExtents, o), "not inside the obstacle");
        }

        [Test]
        public void FlightOutsideObstacleDepth_NoExtraDamage()
        {
            // Same x as the truck case, but in front of the truck's depth band.
            using var h = BarrySlamsPeaux(new Vector2(12f, -12f), new Vector2(13.5f, -12f));
            LockUpAndGrapple(h);
            float aX = h.Player.Position.x;
            TickToLanding(h);
            Assert.That(h.Cpu.Position.x, Is.LessThan(SliceArena.Truck.XMax), "passes the truck's x range");
            Assert.AreEqual(h.Match.LockUps.BodyslamThrowDistance, aX - h.Cpu.Position.x, 1e-3f);
            Assert.AreEqual(0, h.ObstacleHits.Count);
            Assert.IsFalse(h.Match.LockUps.BodyslamThrowBlocked);
            Assert.AreEqual(h.Tuning.bodyslamDamage, h.Hits.Single().Damage, 1e-5f);
        }

        // ================================================================ T1.10 knockout

        [Test]
        public void LandingKnockout_EndsMatch_SlammerStanding()
        {
            using var h = BarrySlamsPeaux(extra: t => t.bodyslamDamage = t.maxHealth + 10f);
            LockUpAndGrapple(h);
            float aX = h.Player.Position.x;
            TickToLanding(h);
            Assert.AreEqual(CombatState.KnockedOut, h.Cpu.State);
            Assert.AreEqual(0f, h.Cpu.Health, 0f);
            Assert.AreEqual(0, h.Cpu.FallSide);
            Assert.IsTrue(h.Match.IsOver);
            Assert.AreEqual(WrestlerId.Player, h.MatchEnds.Single().Winner);
            Assert.AreEqual(CombatState.Standing, h.Player.State, "A already Standing in the frozen match");
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            Assert.AreEqual(-1, h.Player.HorizontalFacing);
            Assert.AreEqual(h.Match.LockUps.BodyslamThrowDistance, aX - h.Cpu.Position.x, 1e-3f, "landed at the end of the path");
            Assert.AreEqual(1, h.Hits.Count);

            Vector2 frozenA = h.Player.Position, frozenB = h.Cpu.Position;
            h.TickSeconds(0.5f, Dt);
            V2Assert.AreEqual(frozenA, h.Player.Position, 0f, "frozen");
            V2Assert.AreEqual(frozenB, h.Cpu.Position, 0f, "no knockout slide after a slam");
        }

        // ================================================================ T1.11 no control

        [Test]
        public void DuringThrow_NeitherWrestlerHasControl()
        {
            using var h = BarrySlamsPeaux();
            h.CpuIdle = false;
            LockUpAndGrapple(h);
            Vector2 aAt = h.Player.Position;
            int aFacing = h.Player.HorizontalFacing, bFacing = h.Cpu.HorizontalFacing;
            int moves = h.MovesStarted.Count, paired = h.PairedMoves.Count;

            int i = 0;
            while (h.Cpu.State == CombatState.Grappled)
            {
                // Mash everything on both sides, holding a direction.
                h.PlayerInput.HeldMove = i % 2 == 0 ? Vector2.left : Vector2.up;
                h.CpuInput.HeldMove = Vector2.right;
                switch (i % 3)
                {
                    case 0: h.PlayerInput.PressStrikeOnce(); h.CpuInput.PressGrappleOnce(); break;
                    case 1: h.PlayerInput.PressGrappleOnce(); h.CpuInput.PressRunOnce(); break;
                    default: h.PlayerInput.PressRunOnce(); h.CpuInput.PressStrikeOnce(); break;
                }
                h.Tick(1, Dt);
                i++;
                if (h.Cpu.State != CombatState.Grappled) break;
                Assert.AreEqual(CombatState.Grappling, h.Player.State);
                V2Assert.AreEqual(aAt, h.Player.Position, 0f, "slammer rooted");
                Assert.AreEqual(aFacing, h.Player.HorizontalFacing);
                Assert.AreEqual(bFacing, h.Cpu.HorizontalFacing);
                Assert.AreEqual(moves, h.MovesStarted.Count, "no moves");
                Assert.AreEqual(paired, h.PairedMoves.Count, "no second paired move");
                Assert.That(i * Dt, Is.LessThan(2f));
            }
            Assert.AreEqual(Landing(h, barry), i * Dt, 2 * Dt);
            V2Assert.AreEqual(aAt, h.Player.Position, 0f, "slammer rooted through the landing");
        }

        // ================================================================ T1.12 overlap after a blocked throw

        [Test]
        public void BlockedNextToSlammer_BlockingSeparatesThem()
        {
            // Slammer 1 unit from the left end, throwing left: B is clamped at the end, overlapping A's feet box.
            using var h = BarrySlamsPeaux(new Vector2(1f, -12f), new Vector2(2.5f, -12f));
            LockUpAndGrapple(h);
            TickToLanding(h);
            Assert.IsTrue(h.Match.LockUps.BodyslamThrowBlocked);
            float gap = Mathf.Abs(h.Player.Position.x - h.Cpu.Position.x);
            Assert.That(gap, Is.LessThan(2f * h.Tuning.feetHalfWidth), "setup: the pair overlaps");

            // A walks away (right) freely.
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(0.5f, Dt);
            Assert.That(h.Player.Position.x - h.Cpu.Position.x, Is.GreaterThanOrEqualTo(2f * h.Tuning.feetHalfWidth - 1e-3f),
                "separated");

            // And once apart, blocking works as usual: walking back into B stops at the feet boxes.
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(0.5f, Dt);
            Assert.That(h.Player.Position.x - h.Cpu.Position.x, Is.GreaterThanOrEqualTo(2f * h.Tuning.feetHalfWidth - 1e-3f),
                "blocked again, no stuck pair");
        }

        // ================================================================ T1.13 read at press

        [Test]
        public void ThrowTuning_ReadAtPress_ReleaseClamped()
        {
            // An edit during the slam does not change it; the next slam uses it.
            using (var h = BarrySlamsPeaux())
            {
                LockUpAndGrapple(h);
                float aX = h.Player.Position.x;
                float drawn = DrawnPx(Entry(h, barry), 0.5f) / Ppu;
                h.Tuning.bodyslamThrowBySlammer[0].landingDistancePx = 300f;
                h.Tuning.bodyslamThrowBySlammer[0].distanceVariance = 0f;
                TickToLanding(h);
                Assert.AreEqual(drawn, aX - h.Cpu.Position.x, 1e-3f, "fixed at the press");

                // Next slam: wait for B to get up, walk in, lock up and slam again (now throwing right).
                h.TickUntil(() => h.Cpu.State == CombatState.Standing, 5f, Dt);
                h.PlayerInput.HeldMove = Vector2.left;
                h.TickUntil(() => h.Player.Position.x - h.Cpu.Position.x < 1.4f, 5f, Dt);
                h.PlayerInput.HeldMove = Vector2.zero;
                LockUpAndGrapple(h);
                Assert.AreEqual(1, h.PairedMoves.Last().ThrowDirection);
                aX = h.Player.Position.x;
                TickToLanding(h);
                Assert.AreEqual(300f / Ppu, h.Cpu.Position.x - aX, 1e-3f, "the edit applies to the next slam");
            }

            // releaseTime above the landing time is clamped to it: no movement until the landing, then the full distance.
            using (var h = BarrySlamsPeaux(extra: t => t.bodyslamThrowBySlammer[0].releaseTime = 5f))
            {
                LockUpAndGrapple(h);
                Vector2 cAt = h.Cpu.Position;
                float aX = h.Player.Position.x;
                while (true)
                {
                    h.Tick(1, Dt);
                    if (h.Cpu.State != CombatState.Grappled) break;
                    V2Assert.AreEqual(cAt, h.Cpu.Position, 0f, "no flight before the clamped release");
                }
                Assert.AreEqual(DrawnPx(Entry(h, barry), 0.5f) / Ppu, aX - h.Cpu.Position.x, 1e-3f);
                Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
            }
        }

        // ================================================================ T1.14 project asset

        private static CombatTuning ProjectTuning(out WrestlerDefinition b, out WrestlerDefinition p)
        {
            var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatTuning>("Assets/Data/Tuning/CombatTuning.asset");
            b = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>("Assets/Data/Wrestlers/Barry.asset");
            p = UnityEditor.AssetDatabase.LoadAssetAtPath<WrestlerDefinition>("Assets/Data/Wrestlers/Peaux.asset");
            Assert.IsNotNull(tuning);
            Assert.IsNotNull(b);
            Assert.IsNotNull(p);
            return tuning;
        }

        [Test]
        public void ProjectAsset_BothSlammersThrow()
        {
            // D-044 (Barry) and D-045 (Peaux, 2026-11-17): both throws on in the shipped asset. D-046: landing distances
            // 1.25x the art-measured 382 / 342 px.
            CombatTuning tuning = ProjectTuning(out WrestlerDefinition b, out WrestlerDefinition p);

            Assert.IsTrue(tuning.TryGetBodyslamThrow(b, out BodyslamThrow e), "Barry throws");
            Assert.AreEqual(b, e.slammer);
            Assert.AreEqual(0.42f, e.releaseTime, 1e-5f);
            Assert.AreEqual(178f, e.releaseOffsetPx, 1e-4f);
            Assert.AreEqual(477.5f, e.landingDistancePx, 1e-4f, "D-046: 1.25 x 382");
            Assert.AreEqual(0.10f, e.distanceVariance, 1e-5f);

            Assert.IsTrue(tuning.TryGetBodyslamThrow(p, out BodyslamThrow f), "Peaux throws");
            Assert.AreEqual(p, f.slammer);
            Assert.AreEqual(0.42f, f.releaseTime, 1e-5f);
            Assert.AreEqual(159f, f.releaseOffsetPx, 1e-4f);
            Assert.AreEqual(427.5f, f.landingDistancePx, 1e-4f, "D-046: 1.25 x 342");
            Assert.AreEqual(0.10f, f.distanceVariance, 1e-5f);

            Assert.AreEqual(0.62f, tuning.BodyslamTimingFor(b).Startup, 1e-5f, "Barry's impact (landing)");
            Assert.AreEqual(0.62f, tuning.BodyslamTimingFor(p).Startup, 1e-5f, "Peaux's impact (landing, his throw clip)");
            Assert.That(e.releaseTime, Is.LessThanOrEqualTo(tuning.BodyslamTimingFor(b).Startup));
            Assert.That(f.releaseTime, Is.LessThanOrEqualTo(tuning.BodyslamTimingFor(p).Startup));
        }

        [Test]
        public void ProjectAssetCopy_PeauxEntryDisabled_OldBodyslam()
        {
            // The no-throw fallback (D-038 path) stays covered: a copy of the project's bodyslam tuning with Peaux's entry
            // switched off. The project asset itself is not modified.
            CombatTuning project = ProjectTuning(out WrestlerDefinition b, out WrestlerDefinition p);
            void CopyWithPeauxOff(CombatTuning t)
            {
                t.bodyslamTiming = project.bodyslamTiming;
                t.bodyslamStartupByInitiator = project.bodyslamStartupByInitiator.ToArray();
                t.bodyslamThrowBySlammer = project.bodyslamThrowBySlammer.ToArray();
                for (int i = 0; i < t.bodyslamThrowBySlammer.Length; i++)
                    if (t.bodyslamThrowBySlammer[i].slammer == p)
                        t.bodyslamThrowBySlammer[i].throwEnabled = false;
            }

            using var h = new MatchHarness(P, C, configure: CopyWithPeauxOff, playerWrestler: p, cpuWrestler: b);
            Assert.IsFalse(h.Tuning.TryGetBodyslamThrow(p, out _), "copy: Peaux's entry disabled");
            Assert.IsTrue(h.Tuning.TryGetBodyslamThrow(b, out _), "copy: Barry still throws");
            Assert.IsTrue(project.TryGetBodyslamThrow(p, out _), "project asset untouched");
            AssertOldBodyslam(h, project.BodyslamTimingFor(p).Startup);
        }

        [Test]
        public void NewTuning_NoThrowForAnyone()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                Assert.AreEqual(0, t.bodyslamThrowBySlammer.Length);
                Assert.IsFalse(t.TryGetBodyslamThrow(barry, out _));
                Assert.IsFalse(t.TryGetBodyslamThrow(null, out _));
            }
            finally { Object.DestroyImmediate(t); }
        }

        // ================================================================ T1.15 CPU

        private sealed class Recording : IWrestlerInput
        {
            private readonly IWrestlerInput inner;
            private readonly Func<CombatState> cpuState;
            public readonly List<(CombatState state, WrestlerIntent intent)> Reads = new List<(CombatState, WrestlerIntent)>();
            public Recording(IWrestlerInput inner, Func<CombatState> cpuState) { this.inner = inner; this.cpuState = cpuState; }
            public WrestlerIntent Read() { WrestlerIntent i = inner.Read(); Reads.Add((cpuState(), i)); return i; }
        }

        private static bool IsIdle(WrestlerIntent i) =>
            i.Move == Vector2.zero && !i.Strike.Pressed && !i.RunWhip.Pressed && !i.LockUpGrapple.Pressed;

        [Test]
        public void CpuSlammer_SameRules()
        {
            // A CPU Barry locks up and slams a player Peaux: same throw rules.
            using (var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12f), configure: t =>
                   {
                       ConfigureThrow(t);
                       t.cpuApproachChance = 1f; t.cpuAttackChance = 1f; t.cpuLockUpChance = 1f; t.cpuBodyslamChance = 1f;
                       t.cpuStompChance = 0f;
                   }, random01: () => 0.5f, playerWrestler: peaux, cpuWrestler: barry))
            {
                var brain = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, 7);
                var rec = new Recording(brain, () => h.Cpu.State);
                h.CpuSource = rec;
                h.TickUntil(() => h.Cpu.State == CombatState.Grappling, 10f, Dt);
                PairedMoveInfo info = h.PairedMoves.Last();
                Assert.AreEqual(MoveId.Bodyslam, info.Move);
                Assert.AreEqual(WrestlerId.Cpu, info.Initiator);
                Assert.AreEqual(-info.Facing, info.ThrowDirection);
                Assert.AreEqual(Landing(h, barry), info.Timing.Startup, 1e-6f);
                float cpuX = h.Cpu.Position.x;
                int from = rec.Reads.Count;
                h.TickUntil(() => h.Player.State != CombatState.Grappled, 2f, Dt);
                Assert.IsTrue(rec.Reads.Skip(from).Where(r => r.state == CombatState.Grappling).All(r => IsIdle(r.intent)),
                    "the brain gives no input during the slam");
                Assert.AreEqual(CombatState.KnockedDown, h.Player.State);
                Assert.AreEqual(DrawnPx(Entry(h, barry), 0.5f) / Ppu, (h.Player.Position.x - cpuX) * info.ThrowDirection, 1e-3f);
                Assert.That(h.Cpu.State, Is.EqualTo(CombatState.Standing).Or.EqualTo(CombatState.Walking).Or.EqualTo(CombatState.Striking));
            }

            // A player Barry slams a CPU Peaux: the brain gives no input while Grappled and mashes once Knocked Down.
            using (var h = BarrySlamsPeaux(extra: t => { t.cpuApproachChance = 0f; t.cpuAttackChance = 0f; t.cpuLockUpChance = 0f; }))
            {
                var brain = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, 7);
                var rec = new Recording(brain, () => h.Cpu.State);
                h.CpuSource = rec;
                LockUpAndGrapple(h);
                TickToLanding(h);
                Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State);
                h.TickUntil(() => h.Cpu.State != CombatState.KnockedDown, 5f, Dt);
                Assert.IsTrue(rec.Reads.Where(r => r.state == CombatState.Grappled).All(r => IsIdle(r.intent)),
                    "no input while Grappled");
                Assert.IsTrue(rec.Reads.Any(r => r.state == CombatState.KnockedDown && r.intent.Strike.Pressed),
                    "mashes once Knocked Down (D-025)");
            }
        }
    }
}
