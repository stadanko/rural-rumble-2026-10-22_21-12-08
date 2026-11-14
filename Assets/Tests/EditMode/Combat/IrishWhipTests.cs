using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// Lock-up slice step 3, the Irish Whip: COMBAT_SPEC Lock Up/Grappling/Irish Whip Rules ("Wrestler A presses
    /// Irish Whip button": direction held, else the way B faces; B runs; an object or a strike puts B in Struck),
    /// Running (whip runs), Tuning Values (whip no-control time 3.0, Irish Whip impact damage 8), D-028 (no control
    /// for 3 s, then Run cancels; whipper free when the whip ends; strikes deal strike damage), D-039 (direction,
    /// choreography, runner keyed path), ARCHITECTURE 6.
    /// Player at (10, -12) facing right locks up the CPU, snapped to (11.2, -12) facing left. With no direction
    /// the whip goes left (the way the CPU faces): "through", past the player. New tunings use Barry's staged
    /// choreography (f1 0.12, f2 0.10, f3 0.12, f4 0.33 s; through -80 / 55 / 98 / 222 px; away 58 / 64 / 98 / 222 px).
    /// </summary>
    public class IrishWhipTests
    {
        private static readonly Vector2 P = new Vector2(10f, -12f);
        private static readonly Vector2 C = new Vector2(11.5f, -12f);
        private const float Dt = 0.01f;
        private const float Ppu = 60f;

        /// <summary>Player locks up the CPU, then presses Irish Whip on the next tick holding <paramref name="stick"/>.</summary>
        private static void LockUpAndWhip(MatchHarness h, Vector2 stick = default)
        {
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            h.PlayerInput.HeldMove = stick;
            h.PlayerInput.PressRunOnce();
            h.Tick(1, Dt);
            h.PlayerInput.Release();
        }

        /// <summary>Expected runner x: whipper x + W x offset (px).</summary>
        private static float RunnerX(float whipperX, int w, float offsetPx) => whipperX + w * offsetPx / Ppu;

        // ================================================================ Start and payload

        [Test]
        public void WhipInLockUp_NoDirection_WhipsThrough_GrapplingAndRunning_RaisesPairedMoveStarted()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);

            Assert.AreEqual(CombatState.Grappling, h.Player.State, "whipper");
            Assert.AreEqual(CombatState.Running, h.Cpu.State, "runner");
            Assert.AreEqual(MoveId.IrishWhip, h.Player.CurrentMove);
            Assert.AreEqual(MoveId.IrishWhip, h.Cpu.CurrentMove);
            Assert.AreEqual(MoveId.IrishWhip, h.Match.LockUps.CurrentMove);
            Assert.IsTrue(h.CpuW.IsWhipRun);
            Assert.IsTrue(h.CpuW.IsWhipNoControl);

            PairedMoveInfo info = h.PairedMoves.Last();
            Assert.AreEqual(MoveId.IrishWhip, info.Move);
            Assert.AreEqual(WrestlerId.Player, info.Initiator);
            Assert.AreEqual(WrestlerId.Cpu, info.Receiver);
            Assert.AreEqual(1, info.Facing, "whipper's lock-up facing");
            Assert.AreEqual(-1, info.WhipDirection, "D-039: no direction = the way the receiver faces");
            Assert.IsTrue(info.WhipThrough, "past the whipper");
            Assert.AreEqual(0.34f, info.Timing.Startup, 1e-5f, "f1 + f2 + f3 = release");
            Assert.AreEqual(0f, info.Timing.Active, 1e-6f);
            Assert.AreEqual(0.33f, info.Timing.Recovery, 1e-5f, "f4 held");

            // Facing: the runner faces the whip direction from the first tick; the whipper's facing is locked.
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);
            Assert.AreEqual(1, h.Player.HorizontalFacing);

            // f1 key snapped on the press tick (through: 80 px in front of the whipper, vs. 72 px in the lock-up).
            V2Assert.AreEqual(P, h.Player.Position, 1e-6f, "whipper rooted");
            V2Assert.AreEqual(new Vector2(RunnerX(P.x, -1, -80f), P.y), h.Cpu.Position, 1e-4f, "runner f1");
            V2Assert.AreEqual((h.Player.Position + h.Cpu.Position) * 0.5f, info.Anchor, 1e-5f, "anchor");

            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Player, CombatState.LockUp, CombatState.Grappling)));
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.LockedUp, CombatState.Running)));
            Assert.IsFalse(h.MovesStarted.Any(m => m.move == MoveId.IrishWhip), "a paired move raises no MoveStarted");
            Assert.IsFalse(h.PairedMoves.Any(m => m.Move == MoveId.BreakLockUp));
        }

        [TestCase(1f, 0f, 1, false, TestName = "HeldRight_WhipsRight_Away")]
        [TestCase(-1f, 0f, -1, true, TestName = "HeldLeft_WhipsLeft_Through")]
        [TestCase(0f, 1f, -1, true, TestName = "UpOnly_CountsAsNoDirection")]
        [TestCase(0f, -1f, -1, true, TestName = "DownOnly_CountsAsNoDirection")]
        [TestCase(0.7f, 0.7f, 1, false, TestName = "UpRight_UsesTheLeftRightPart")]
        [TestCase(0.2f, 0f, -1, true, TestName = "InsideDeadzone_CountsAsNoDirection")]
        public void WhipDirection_FromHeldLeftRight_ElseReceiverFacing(float x, float y, int expectedDir, bool expectedThrough)
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h, new Vector2(x, y));
            PairedMoveInfo info = h.PairedMoves.Last();
            Assert.AreEqual(expectedDir, info.WhipDirection);
            Assert.AreEqual(expectedThrough, info.WhipThrough);
            Assert.AreEqual(expectedDir, h.Cpu.HorizontalFacing, "runner faces the whip direction at once");
            Assert.AreEqual(1, h.Player.HorizontalFacing, "whipper facing locked");
        }

        [Test]
        public void WhipDirectionFor_PureRule()
        {
            Assert.AreEqual(1, LockUpController.WhipDirectionFor(0.5f, 0.3f, -1));
            Assert.AreEqual(-1, LockUpController.WhipDirectionFor(-0.5f, 0.3f, 1));
            Assert.AreEqual(-1, LockUpController.WhipDirectionFor(0f, 0.3f, -1));
            Assert.AreEqual(1, LockUpController.WhipDirectionFor(0.3f, 0.3f, 1), "at the deadzone: none");
        }

        // ================================================================ Runner path (D-039 keys)

        [Test]
        public void RunnerPath_Through_HoldsEachFrameKey_ThenRunsFromF4()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            float rs = h.Tuning.runSpeed;
            // (ticks after the press tick, expected offset in px along W = -1)
            (int ticks, float px)[] samples =
            {
                (6, -80f),   // t 0.06, f1
                (11, -80f),  // t 0.11, f1
                (17, 55f),   // t 0.17, f2
                (28, 98f),   // t 0.28, f3
                (40, 222f + (0.40f - 0.34f) * rs * Ppu), // t 0.40, f4: from the f4 key at runSpeed
                (60, 222f + (0.60f - 0.34f) * rs * Ppu), // t 0.60
            };
            int done = 0;
            foreach (var (ticks, px) in samples)
            {
                h.Tick(ticks - done, Dt);
                done = ticks;
                Assert.AreEqual(RunnerX(P.x, -1, px), h.Cpu.Position.x, 1e-3f, $"runner x at t = {ticks * Dt}");
                Assert.AreEqual(P.y, h.Cpu.Position.y, 1e-6f, "same depth");
                V2Assert.AreEqual(P, h.Player.Position, 1e-6f, "whipper rooted");
                Assert.AreEqual(CombatState.Running, h.Cpu.State);
            }
        }

        [Test]
        public void RunnerPath_Away_HoldsEachFrameKey()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h, new Vector2(1f, 0f));
            V2Assert.AreEqual(new Vector2(RunnerX(P.x, 1, 58f), P.y), h.Cpu.Position, 1e-4f, "f1 snap (58 px, lock-up 72 px)");
            h.Tick(17, Dt);
            Assert.AreEqual(RunnerX(P.x, 1, 64f), h.Cpu.Position.x, 1e-3f, "f2");
            h.Tick(11, Dt);
            Assert.AreEqual(RunnerX(P.x, 1, 98f), h.Cpu.Position.x, 1e-3f, "f3");
            h.Tick(12, Dt);
            Assert.AreEqual(RunnerX(P.x, 1, 222f + 0.06f * h.Tuning.runSpeed * Ppu), h.Cpu.Position.x, 1e-3f, "f4");
        }

        [Test]
        public void PerWhipper_Choreography_FromTheWhippersDefinition()
        {
            var barry = ScriptableObject.CreateInstance<WrestlerDefinition>();
            var peaux = ScriptableObject.CreateInstance<WrestlerDefinition>();
            try
            {
                void Configure(CombatTuning t)
                {
                    IrishWhipChoreography b = IrishWhipChoreography.BarryStaged; b.whipper = barry;
                    IrishWhipChoreography p = IrishWhipChoreography.PeauxStaged; p.whipper = peaux;
                    t.irishWhipByWhipper = new[] { b, p };
                }

                // Player = Peaux whips CPU = Barry, through.
                using (var h = new MatchHarness(P, C, configure: Configure, playerWrestler: peaux, cpuWrestler: barry))
                {
                    LockUpAndWhip(h);
                    PairedMoveInfo info = h.PairedMoves.Last();
                    Assert.AreEqual(0.25f, info.Timing.Recovery, 1e-5f, "Peaux's f4");
                    Assert.AreEqual(RunnerX(P.x, -1, -55f), h.Cpu.Position.x, 1e-4f, "Peaux's through f1");
                    h.Tick(28, Dt);
                    Assert.AreEqual(RunnerX(P.x, -1, 80f), h.Cpu.Position.x, 1e-3f, "Peaux's through f3");
                    float t = h.TickUntil(() => !h.Match.LockUps.IsActive, 2f, Dt);
                    Assert.AreEqual(0.59f - 0.28f, t, 2 * Dt, "f1 + f2 + f3 + Peaux's f4");
                }

                // Player = Barry whips CPU = Peaux, away.
                using (var h = new MatchHarness(P, C, configure: Configure, playerWrestler: barry, cpuWrestler: peaux))
                {
                    LockUpAndWhip(h, new Vector2(1f, 0f));
                    Assert.AreEqual(0.33f, h.PairedMoves.Last().Timing.Recovery, 1e-5f, "Barry's f4");
                    Assert.AreEqual(RunnerX(P.x, 1, 58f), h.Cpu.Position.x, 1e-4f, "Barry's away f1");
                }
            }
            finally
            {
                Object.DestroyImmediate(barry);
                Object.DestroyImmediate(peaux);
            }
        }

        // ================================================================ Pass-through, end of the whip

        [Test]
        public void RunnerPassesTheWhipper_NoRunCollision_NoBlocking()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            bool overlappedBlockingBox = false;
            h.TickUntil(() =>
            {
                if (Mathf.Abs(h.Cpu.Position.x - h.Player.Position.x) < 2f * h.Tuning.feetHalfWidth)
                    overlappedBlockingBox = true;
                return !h.Match.LockUps.IsActive;
            }, 2f, Dt);

            Assert.IsTrue(overlappedBlockingBox, "the f2 key is inside the whipper's blocking box: passed through it");
            Assert.Less(h.Cpu.Position.x, h.Player.Position.x - 3f, "runner ends up well past the whipper");
            Assert.AreEqual(0, h.KnockDowns.Count, "no D-029 run collision");
            Assert.AreEqual(0, h.Hits.Count);
            Assert.AreEqual(0, h.ObstacleHits.Count);
            Assert.AreEqual(h.Tuning.maxHealth, h.Player.Health, 1e-6f);
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health, 1e-6f);
        }

        [Test]
        public void WhipEnds_WhipperFree_FacingWhipDirection_RunnerKeepsRunning()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            float t = h.TickUntil(() => !h.Match.LockUps.IsActive, 2f, Dt);
            Assert.AreEqual(0.67f, t, 2 * Dt, "f1 + f2 + f3 + f4 after the press tick");

            Assert.AreEqual(CombatState.Standing, h.Player.State, "D-028: the whipper is free when the whip ends");
            Assert.AreEqual(MoveId.None, h.Player.CurrentMove);
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "D-039: faces the whip direction");
            V2Assert.AreEqual(P, h.Player.Position, 1e-6f, "rooted for the whole whip");

            Assert.AreEqual(CombatState.Running, h.Cpu.State);
            Assert.AreEqual(MoveId.None, h.Cpu.CurrentMove, "off the whip path");
            Assert.IsTrue(h.CpuW.IsWhipRun);
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing);

            var playerPhases = h.PhaseChanges.Where(e => e.id == WrestlerId.Player).Select(e => (e.move, e.phase)).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                (MoveId.LockUp, MovePhase.None),
                (MoveId.IrishWhip, MovePhase.Active), (MoveId.IrishWhip, MovePhase.Recovery), (MoveId.IrishWhip, MovePhase.None)
            }, playerPhases);
            var cpuPhases = h.PhaseChanges.Where(e => e.id == WrestlerId.Cpu).Select(e => (e.move, e.phase)).ToArray();
            CollectionAssert.AreEqual(playerPhases, cpuPhases, "runner mirrors the whipper's phases while on the path");

            // Whipper has control again: walks.
            h.PlayerInput.HeldMove = new Vector2(1f, 0f);
            h.Tick(5, Dt);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "Focus keeps facing the runner (D-037)");
        }

        [Test]
        public void WhipRun_EndsAtTheArenaEnd_WithoutDamage()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            h.TickUntil(() => h.Cpu.State != CombatState.Running, 3f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State, "D-027: arena ends just end the run");
            Assert.AreEqual(SliceArena.BandXMin + h.Tuning.feetHalfWidth, h.Cpu.Position.x, 1e-3f);
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health, 1e-6f);
            Assert.AreEqual(0, h.ObstacleHits.Count);
            Assert.IsFalse(h.CpuW.IsWhipRun);
        }

        // ================================================================ No control for 3 s (D-028)

        [Test]
        public void WhippedWrestler_NoControlForNoControlTime_ThenRunCancels()
        {
            using var h = new MatchHarness(P, C, arena: ArenaGeometry.Unbounded());
            h.CpuIdle = false;
            LockUpAndWhip(h);
            int movesBefore = h.MovesStarted.Count;

            int ticks = Mathf.RoundToInt(h.Tuning.whipNoControlTime / Dt) - 3;
            for (int i = 0; i < ticks; i++)
            {
                var mash = new WrestlerIntent(new Vector2(1f, 1f),
                    new ButtonState(i % 3 == 0, true), new ButtonState(i % 3 == 1, true), new ButtonState(i % 3 == 2, true));
                h.CpuInput.Enqueue(mash);
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.Running, h.Cpu.State, $"no control at tick {i}");
            }
            Assert.IsTrue(h.CpuW.IsWhipNoControl);
            Assert.AreEqual(movesBefore, h.MovesStarted.Count, "no strike, lock-up or whiff");
            Assert.AreEqual(-1, h.Cpu.HorizontalFacing, "kept running the whip direction");

            h.CpuInput.Wait();
            h.CpuInput.Wait();
            h.CpuInput.Wait();
            h.CpuInput.Wait();
            h.Tick(4, Dt);
            Assert.IsFalse(h.CpuW.IsWhipNoControl, "no-control time over");
            Assert.AreEqual(CombatState.Running, h.Cpu.State, "the run goes on by itself");

            h.CpuInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State, "D-028: after the no-control time Run cancels the whip run");
        }

        [Test]
        public void AfterNoControlTime_StrikeEndsTheWhipRun_WithStrike1()
        {
            using var h = new MatchHarness(P, C, arena: ArenaGeometry.Unbounded());
            h.CpuIdle = false;
            LockUpAndWhip(h);
            h.TickSeconds(h.Tuning.whipNoControlTime + 0.05f, Dt);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);
            h.CpuInput.PressStrikeOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Cpu.State);
            Assert.AreEqual(MoveId.Strike1, h.Cpu.CurrentMove, "D-029: normal run rules after the no-control time");
        }

        [Test]
        public void NoControlTime_IsReadAtUse()
        {
            using var h = new MatchHarness(P, C, arena: ArenaGeometry.Unbounded());
            h.CpuIdle = false;
            LockUpAndWhip(h);
            h.Tuning.whipNoControlTime = 1.0f; // Play Mode edit mid-run
            h.TickSeconds(1.05f, Dt);
            h.CpuInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
        }

        // ================================================================ Obstacles, arena end at the start

        [Test]
        public void WhipRun_IntoObstacleAfterTheWhip_IrishWhipImpactDamage_Struck()
        {
            var arena = new ArenaGeometry(SliceArena.Band, new[] { new ArenaRect(2f, -12.5f, 3f, -11.5f) });
            using var h = new MatchHarness(P, C, arena: arena, configure: t =>
            {
                t.irishWhipImpactDamage = 11f;
                t.obstacleCollisionDamage = 5f;
            });
            LockUpAndWhip(h);
            h.TickUntil(() => h.ObstacleHits.Count > 0, 3f, Dt);
            Assert.IsFalse(h.Match.LockUps.IsActive, "the whip itself was over");
            Assert.AreEqual(WrestlerId.Cpu, h.ObstacleHits.Single().id);
            Assert.AreEqual(h.Tuning.maxHealth - 11f, h.Cpu.Health, 1e-4f, "COMBAT_SPEC Irish Whip impact damage, not obstacle damage");
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
        }

        [Test]
        public void WhipPath_KeyStoppedByObstacle_ImpactDuringTheWhip_WhipperPlaysOn()
        {
            // An obstacle between the f3 key (8.37) and the f4 key (6.30).
            var arena = new ArenaGeometry(SliceArena.Band, new[] { new ArenaRect(7f, -12.5f, 7.5f, -11.5f) });
            using var h = new MatchHarness(P, C, arena: arena);
            LockUpAndWhip(h);
            float t = h.TickUntil(() => h.ObstacleHits.Count > 0, 2f, Dt);
            Assert.AreEqual(0.34f, t, 2 * Dt, "at the release (f4 key)");
            Assert.AreEqual(h.Tuning.maxHealth - h.Tuning.irishWhipImpactDamage, h.Cpu.Health, 1e-4f);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State);
            Assert.AreEqual(7.5f + h.Tuning.feetHalfWidth, h.Cpu.Position.x, 0.01f, "stopped at the obstacle");
            Assert.IsTrue(h.PhaseChanges.Contains((WrestlerId.Cpu, MoveId.IrishWhip, MovePhase.None)), "off the path");

            Assert.AreEqual(CombatState.Grappling, h.Player.State, "whipper's whip plays on");
            Assert.IsTrue(h.Match.LockUps.IsActive);
            h.TickUntil(() => !h.Match.LockUps.IsActive, 1f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
        }

        [Test]
        public void WhipImpact_BelowThreshold_KnocksDown()
        {
            var arena = new ArenaGeometry(SliceArena.Band, new[] { new ArenaRect(2f, -12.5f, 3f, -11.5f) });
            using var h = new MatchHarness(P, C, arena: arena, configure: t => t.maxHealth = 35f); // 35 - 8 = 27
            LockUpAndWhip(h);
            h.TickUntil(() => h.ObstacleHits.Count > 0, 3f, Dt);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State, "D-030: below 30 after the impact");
            Assert.AreEqual(27f, h.Cpu.Health, 1e-4f);
        }

        [Test]
        public void WhipThroughNearArenaEnd_RunEndsAtTheEnd_NoDamage_WhipperPlaysOn()
        {
            // Whipper 1.5 from the left end; the through path's f3 key is past the end.
            using var h = new MatchHarness(new Vector2(1.5f, -12f), new Vector2(3f, -12f));
            LockUpAndWhip(h);
            float t = h.TickUntil(() => h.Cpu.State != CombatState.Running, 1f, Dt);
            Assert.AreEqual(0.22f, t, 2 * Dt, "stopped when the f3 key would cross the arena end");
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(SliceArena.BandXMin + h.Tuning.feetHalfWidth, h.Cpu.Position.x, 1e-3f);
            Assert.AreEqual(h.Tuning.maxHealth, h.Cpu.Health, 1e-6f);
            Assert.AreEqual(CombatState.Grappling, h.Player.State);

            h.TickUntil(() => !h.Match.LockUps.IsActive, 1f, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(0, h.KnockDowns.Count);
        }

        // ================================================================ Strikes on a whipped wrestler (D-028)

        [Test]
        public void StrikeOnWhippedRunner_DealsNormalStrikeDamage()
        {
            using var h = new MatchHarness(P, C, configure: t =>
            {
                t.runSpeed = 0.5f;
                t.irishWhip.throughPx = new WhipRunnerOffsets(-80f, 55f, 98f, 100f); // ends ~1.8 units past the whipper
            });
            LockUpAndWhip(h);
            h.TickUntil(() => !h.Match.LockUps.IsActive, 2f, Dt);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);

            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Hits.Count > 0, 1f, Dt);
            HitInfo hit = h.Hits.Single();
            Assert.AreEqual(MoveId.Strike1, hit.Move);
            Assert.AreEqual(h.Tuning.strike1Damage, hit.Damage, 1e-5f, "strike damage, not Irish Whip impact damage");
            Assert.AreEqual(CombatState.Struck, h.Cpu.State, "COMBAT_SPEC: a strike puts the runner in Struck");
        }

        // ================================================================ Who can whip, when

        [Test]
        public void CpuInitiator_SameRules()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.CpuInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.LockedUp, h.Player.State);
            h.CpuInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Grappling, h.Cpu.State);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            PairedMoveInfo info = h.PairedMoves.Last();
            Assert.AreEqual(WrestlerId.Cpu, info.Initiator);
            Assert.AreEqual(1, info.WhipDirection, "the way the player faces (toward the CPU)");
            Assert.IsTrue(info.WhipThrough);
            Assert.IsTrue(h.PlayerW.IsWhipNoControl);
        }

        [Test]
        public void LockedUpWrestlerPressingWhip_DoesNothing()
        {
            using var h = new MatchHarness(P, C);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.CpuInput.Wait();
            h.Tick(1, Dt);
            for (int i = 0; i < 10; i++)
            {
                h.CpuInput.PressRunOnce();
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State, "Wrestler B has no control (D-031)");
        }

        [Test]
        public void WhipDuringBreak_DoesNothing()
        {
            using var h = new MatchHarness(P, C);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            h.TickUntil(() => h.Match.LockUps.CurrentMove == MoveId.BreakLockUp, 3f, Dt);
            h.PlayerInput.PressRunOnce();
            h.Tick(1, Dt);
            Assert.AreEqual(MoveId.BreakLockUp, h.Match.LockUps.CurrentMove);
            Assert.IsFalse(h.PairedMoves.Any(m => m.Move == MoveId.IrishWhip));
        }

        [Test]
        public void DuringWhip_WhipperHasNoControl()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            int movesBefore = h.MovesStarted.Count;
            int pairedBefore = h.PairedMoves.Count;
            for (int i = 0; i < 40; i++)
            {
                h.PlayerInput.Enqueue(new WrestlerIntent(new Vector2(1f, 1f),
                    new ButtonState(i % 3 == 0, true), new ButtonState(i % 3 == 1, true), new ButtonState(i % 3 == 2, true)));
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.Grappling, h.Player.State);
                V2Assert.AreEqual(P, h.Player.Position, 1e-6f);
                Assert.AreEqual(1, h.Player.HorizontalFacing, "facing locked during the whip");
            }
            Assert.AreEqual(movesBefore, h.MovesStarted.Count);
            Assert.AreEqual(pairedBefore, h.PairedMoves.Count);
        }

        // ================================================================ Tuning

        [Test]
        public void NewTuning_HasIrishWhipDefaults()
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                Assert.AreEqual(3.0f, t.whipNoControlTime, 1e-6f, "COMBAT_SPEC Set (D-028)");
                Assert.AreEqual(8f, t.irishWhipImpactDamage, 1e-6f, "COMBAT_SPEC Starting point");
                Assert.AreEqual(0.5f, t.cpuWhipChance, 1e-6f);
                MoveTiming w = t.TimingFor(MoveId.IrishWhip);
                Assert.AreEqual(0.34f, w.Startup, 1e-5f);
                Assert.AreEqual(0.33f, w.Recovery, 1e-5f);
                Assert.AreEqual(-80f, t.IrishWhipFor(null).throughPx.f1, 1e-6f);
                Assert.AreEqual(58f, t.IrishWhipFor(null).awayPx.f1, 1e-6f);
            }
            finally { Object.DestroyImmediate(t); }
        }

        [Test]
        public void WhipTiming_IsReadAtUse()
        {
            using var h = new MatchHarness(P, C);
            LockUpAndWhip(h);
            h.Tuning.irishWhip.followThrough = 0.1f; // Play Mode edit mid-whip
            float t = h.TickUntil(() => !h.Match.LockUps.IsActive, 2f, Dt);
            Assert.AreEqual(0.44f, t, 2 * Dt);
        }
    }
}
