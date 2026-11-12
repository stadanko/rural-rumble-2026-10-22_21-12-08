using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-037 / COMBAT_SPEC "Focus (D-037)" / CONTROLS Run and Focus, through CombatMatch:
    /// - free wrestlers (Standing, Walking, Recovering) face the opponent left/right every tick, whatever
    ///   direction they move; back stance only when the opponent is MORE than focusBackStanceHeightPx higher;
    /// - facing is locked while Striking, Struck, Knocked Down / Out, in a lock-up (or whiff) or bodyslam, and
    ///   snaps back to the opponent when the wrestler is free again;
    /// - the runner faces the run direction; a non-running wrestler keeps focusing on a runner;
    /// - the CPU follows the same rules.
    /// Thresholds and speeds come from the harness's CombatTuning instance; positions are chosen relative to them.
    /// Expected facings are written from the spec (<see cref="Expected"/>), not from FocusRules.
    /// </summary>
    public class FocusTests
    {
        private const float Dt = 0.02f;

        /// <summary>Back-stance height in world units (D-037: tuning px, 60 px = 1 unit).</summary>
        private static float Threshold(MatchHarness h) => h.Tuning.focusBackStanceHeightPx / 60f;

        /// <summary>The spec's Focus facing for <paramref name="self"/> looking at <paramref name="opp"/> (opp.x != self.x).</summary>
        private static Vector2 Expected(Vector2 self, Vector2 opp, float threshold)
        {
            float x = opp.x > self.x ? 1f : -1f;
            return opp.y - self.y > threshold ? new Vector2(x, 1f).normalized : new Vector2(x, 0f);
        }

        /// <summary>Reads a value from a fresh CombatTuning (the instance MatchHarness builds), before a match exists.</summary>
        private static float FromTuning(System.Func<CombatTuning, float> read)
        {
            var t = ScriptableObject.CreateInstance<CombatTuning>();
            try { return read(t); }
            finally { Object.DestroyImmediate(t); }
        }

        /// <summary>Default back-stance height in world units, before a match exists.</summary>
        private static float DefaultThreshold => FromTuning(t => t.focusBackStanceHeightPx / 60f);

        /// <summary>Depth gap at which the two wrestlers can pass each other: just more than the D-027 blocking
        /// depth (2 x wrestlerBlockHalfDepth). With default tuning this is beyond the strike depth tolerance, so a
        /// test that needs a passed opponent to be inside strike reach widens strikeDepthTolerance itself.</summary>
        private static float PassGap => FromTuning(t => 2f * t.wrestlerBlockHalfDepth + 0.1f);

        /// <summary>Walks the player up past the blocking depth, then right until it is 1 unit past the CPU.</summary>
        private static void WalkPlayerAroundToTheRightOf(MatchHarness h)
        {
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickUntil(() => h.Player.Position.y > h.Cpu.Position.y + PassGap, 2f, Dt);
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickUntil(() => h.Player.Position.x > h.Cpu.Position.x + 1f, 3f, Dt);
            h.PlayerInput.Release();
        }

        // ================================================================ Free wrestlers face each other

        // COMBAT_SPEC Focus: facing and stance at match start (stance included), on both sides.
        [TestCase(1)]
        [TestCase(-1)]
        public void MatchStart_FacesEachOther_IncludingBackStance(int playerSide)
        {
            float t = DefaultThreshold;
            var p = new Vector2(13f, -13.4f);
            var c = new Vector2(13f - playerSide * 5f, p.y + t + 0.2f);
            using var h = new MatchHarness(p, c);
            Assert.AreEqual(t, Threshold(h), 1e-5f, "precondition: the harness uses the same tuning values");
            Assert.AreEqual(Expected(p, c, t), h.Player.Facing, "player: opponent much higher -> back stance toward them");
            Assert.IsTrue(h.PlayerW.IsBackFacing);
            Assert.AreEqual(-playerSide, h.Player.HorizontalFacing);
            Assert.AreEqual(new Vector2(playerSide, 0f), h.Cpu.Facing, "CPU: opponent lower -> front stance toward them");
            Assert.IsFalse(h.CpuW.IsBackFacing);
        }

        // COMBAT_SPEC Focus: "Moving away means backing up while still facing the opponent."
        [Test]
        public void WalkingAway_KeepsFacingOpponent_EveryTick()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor, cpuStart: new Vector2(SliceArena.OpenFloor.x + 3f, SliceArena.OpenFloor.y));
            h.PlayerInput.HeldMove = Vector2.left;
            float x0 = h.Player.Position.x;
            for (int i = 0; i < 50; i++)
            {
                h.Tick(1, Dt);
                Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing, $"tick {i}");
                Assert.AreEqual(new Vector2(-1f, 0f), h.Cpu.Facing, $"tick {i}");
            }
            Assert.Less(h.Player.Position.x, x0 - 1f, "precondition: backed away");
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            Assert.IsEmpty(h.FacingChanges);
        }

        // COMBAT_SPEC Focus: crossing over flips both wrestlers, each exactly once, at the crossing.
        [Test]
        public void CrossingOver_FlipsBoth_OnceEach()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(13f, p.y - PassGap));

            h.PlayerInput.HeldMove = Vector2.right;
            for (int i = 0; i < 150 && h.Player.Position.x < 16f; i++)
            {
                h.Tick(1, Dt);
                float dx = h.Cpu.Position.x - h.Player.Position.x;
                if (dx != 0f)
                {
                    Assert.AreEqual(dx > 0f ? 1 : -1, h.Player.HorizontalFacing, $"tick {i} player");
                    Assert.AreEqual(dx > 0f ? -1 : 1, h.Cpu.HorizontalFacing, $"tick {i} CPU");
                }
            }
            Assert.Greater(h.Player.Position.x, h.Cpu.Position.x + 1f, "precondition: the player crossed over");
            CollectionAssert.AreEqual(new[] { (WrestlerId.Player, new Vector2(-1f, 0f)) },
                h.FacingChanges.Where(e => e.id == WrestlerId.Player).ToArray());
            CollectionAssert.AreEqual(new[] { (WrestlerId.Cpu, new Vector2(1f, 0f)) },
                h.FacingChanges.Where(e => e.id == WrestlerId.Cpu).ToArray());
        }

        // COMBAT_SPEC Focus stance: the opponent moves more than the threshold higher -> back stance for this
        // wrestler (the opponent stays front); back to front when they come down again. Checked every tick
        // against the positions after the tick (the boundary itself: "more than" -> back).
        [Test]
        public void OpponentMovesHigher_BackStance_ThenFrontAgain()
        {
            var p = new Vector2(10f, -13.5f);
            var c = new Vector2(14f, -13.5f);
            using var h = new MatchHarness(p, c);
            float t = Threshold(h);
            h.CpuIdle = false;

            bool sawBack = false;
            h.CpuInput.HeldMove = Vector2.up;
            for (int i = 0; i < 200 && h.Cpu.Position.y - h.Player.Position.y < t + 0.3f; i++)
            {
                h.Tick(1, Dt);
                Assert.AreEqual(Expected(h.Player.Position, h.Cpu.Position, t), h.Player.Facing, $"up tick {i}");
                Assert.AreEqual(new Vector2(-1f, 0f), h.Cpu.Facing, $"up tick {i}: the higher wrestler stays front");
                sawBack |= h.PlayerW.IsBackFacing;
            }
            Assert.IsTrue(sawBack && h.PlayerW.IsBackFacing, "back stance once the opponent is more than the threshold higher");
            Assert.AreEqual(new Vector2(1f, 1f).normalized, h.Player.Facing);

            h.CpuInput.HeldMove = Vector2.down;
            for (int i = 0; i < 200 && h.Cpu.Position.y > p.y + 0.1f; i++)
            {
                h.Tick(1, Dt);
                Assert.AreEqual(Expected(h.Player.Position, h.Cpu.Position, t), h.Player.Facing, $"down tick {i}");
            }
            Assert.IsFalse(h.PlayerW.IsBackFacing, "front again at about equal height");
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing);
            // Exactly one change into back stance and one back out, for the player only.
            Assert.AreEqual(2, h.FacingChanges.Count(e => e.id == WrestlerId.Player));
            Assert.AreEqual(0, h.FacingChanges.Count(e => e.id == WrestlerId.Cpu));
        }

        // D-037 tunable "editable ... live in Play Mode": the threshold is read every tick.
        [Test]
        public void BackStanceHeight_EditedMidMatch_AppliesNextTick()
        {
            using var h = new MatchHarness(new Vector2(10f, -13.4f), new Vector2(14f, -13.4f + 0.8f * DefaultThreshold));
            float px = h.Tuning.focusBackStanceHeightPx;
            h.Tick(1, Dt);
            Assert.IsFalse(h.PlayerW.IsBackFacing, "precondition: under the threshold");

            h.Tuning.focusBackStanceHeightPx = px * 0.5f;
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsBackFacing, "lower threshold: now more than it");

            h.Tuning.focusBackStanceHeightPx = px;
            h.Tick(1, Dt);
            Assert.IsFalse(h.PlayerW.IsBackFacing, "threshold restored");
        }

        // COMBAT_SPEC Focus "Strikes and lock-ups reach in the facing direction, so while free to act they always
        // reach toward the opponent": a strike pressed while walking away aims at (and lands on) the opponent.
        [Test]
        public void Strike_PressedWhileWalkingAway_AimsAtOpponent_AndLands()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(12f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(2, Dt);
            h.PlayerInput.PressStrikeOnce(); // still holding left
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "the strike started facing the opponent");
            h.TickSeconds(h.Tuning.strike1Timing.Total, Dt);
            Assert.AreEqual(1, h.Hits.Count, "Strike 1 lands on the opponent in reach");
            Assert.AreEqual(WrestlerId.Cpu, h.Hits[0].Defender);
        }

        // Same rule for Lock Up: pressed while backing away, it reaches toward the opponent.
        [Test]
        public void LockUp_PressedWhileWalkingAway_AimsAtOpponent_AndLocksUp()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(11.5f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(1, 0.01f);
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, 0.01f);
            Assert.AreEqual(CombatState.LockUp, h.Player.State);
            Assert.AreEqual(CombatState.LockedUp, h.Cpu.State);
            Assert.IsFalse(h.PlayerW.IsWhiffing);
        }

        // ================================================================ Locked facing

        // COMBAT_SPEC Focus "Locked facing ... while Striking": the opponent crosses behind during the wind-up;
        // facing does not change until the strike ends, then snaps to the opponent on the tick it is free.
        [Test]
        public void Striking_OpponentCrossesOver_FacingLockedUntilFree()
        {
            var p = new Vector2(10f, -12f);
            float gap = PassGap;
            // Wider depth tolerance (test-only) so the CPU, passing at another depth, is inside the strike's depth band.
            using var h = new MatchHarness(p, new Vector2(14.5f, p.y + gap), configure: t =>
            {
                t.strike1Timing = new MoveTiming(1.5f, 0.1f, 0.15f);
                t.strikeDepthTolerance = gap + 0.2f;
            });
            h.CpuIdle = false;
            h.PlayerInput.PressStrikeOnce();
            h.CpuInput.PressRunOnce(); // nothing held: toward the player (left)
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Striking, h.Player.State);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);

            h.TickUntil(() => h.Cpu.Position.x < p.x - 1.5f, 2f, Dt);
            h.CpuInput.PressRunOnce(); // cancel the run behind the player
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(1, h.Cpu.HorizontalFacing, "the free CPU faces the player again");

            while (h.Player.State == CombatState.Striking)
            {
                Assert.AreEqual(1, h.Player.HorizontalFacing, "locked while Striking");
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "re-aims the tick the strike ends");
            Assert.IsEmpty(h.Hits, "the strike reached in its locked direction, away from the opponent");
        }

        // COMBAT_SPEC Focus "Locked facing ... Struck".
        [Test]
        public void Struck_OpponentCrossesOver_FacingLockedUntilFree()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(12f, p.y), configure: t => t.struckDuration = 2.5f);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.Struck, 1f, Dt);
            int cpuFacing = h.Cpu.HorizontalFacing;
            Assert.AreEqual(-1, cpuFacing);

            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            WalkPlayerAroundToTheRightOf(h);
            Assert.AreEqual(CombatState.Struck, h.Cpu.State, "precondition: crossed over while the CPU is still Struck");
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "the free player faces the CPU on its new side");

            while (h.Cpu.State == CombatState.Struck)
            {
                Assert.AreEqual(cpuFacing, h.Cpu.HorizontalFacing, "locked while Struck");
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(1, h.Cpu.HorizontalFacing, "re-aims the tick Struck ends");
        }

        // COMBAT_SPEC Focus "Locked facing ... Knocked Down"; free again as Recovering (a free state).
        [Test]
        public void KnockedDown_OpponentCrossesOver_FacingLockedUntilUp_ThenRecoveringFaces()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(12f, p.y), configure: t =>
            {
                t.maxHealth = t.recoveringThreshold + t.strike1Damage - 1f; // one Strike 1 drops below the threshold
                t.minKnockdownTime = 3f;
                t.maxKnockdownTime = 3f;
                t.recoveryRate = 0f;
            });
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.KnockedDown, 1f, Dt);
            int cpuFacing = h.Cpu.HorizontalFacing;

            h.TickUntil(() => h.Player.State == CombatState.Standing, 1f, Dt);
            WalkPlayerAroundToTheRightOf(h);
            Assert.AreEqual(CombatState.KnockedDown, h.Cpu.State, "precondition: crossed while the CPU is down");

            while (h.Cpu.State == CombatState.KnockedDown)
            {
                Assert.AreEqual(cpuFacing, h.Cpu.HorizontalFacing, "locked while Knocked Down");
                h.Tick(1, Dt);
            }
            Assert.AreEqual(CombatState.Recovering, h.Cpu.State);
            Assert.AreEqual(-cpuFacing, h.Cpu.HorizontalFacing, "re-aims the tick it is up (Recovering is free to act)");
        }

        // COMBAT_SPEC Focus "Locked facing ... in a lock-up": a Lock Up whiff (D-031) is a lock-up state.
        [Test]
        public void LockUpWhiff_OpponentCrossesOver_FacingLockedUntilFree()
        {
            var p = new Vector2(10f, -12f);
            using var h = new MatchHarness(p, new Vector2(14f, p.y + PassGap), configure: t => t.lockUpWhiffTime = 2.5f);
            h.CpuIdle = false;
            h.PlayerInput.PressGrappleOnce();
            h.Tick(1, Dt);
            Assert.IsTrue(h.PlayerW.IsWhiffing, "precondition: out of reach -> whiff");

            h.CpuInput.HeldMove = Vector2.left;
            h.TickUntil(() => h.Cpu.Position.x < p.x - 1f, 3f, Dt);
            h.CpuInput.Release();
            Assert.IsTrue(h.PlayerW.IsWhiffing, "precondition: crossed during the whiff");
            Assert.AreEqual(1, h.Cpu.HorizontalFacing, "the walking CPU kept facing the player as it passed");

            while (h.Player.State == CombatState.LockUp)
            {
                Assert.AreEqual(1, h.Player.HorizontalFacing, "locked while whiffing");
                h.Tick(1, Dt);
            }
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "re-aims the tick the whiff ends");
        }

        // ================================================================ Running

        // COMBAT_SPEC Focus "Running: Focus is off for the runner, who faces the run direction": running away from
        // the opponent faces away the whole run; cancelling the run (D-027) turns back to the opponent at once.
        [Test]
        public void RunningAway_FacesRunDirection_ThenFacesOpponentWhenRunEnds()
        {
            using var h = new MatchHarness(new Vector2(14f, -12f), new Vector2(20f, -12.6f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.PlayerInput.PressRunOnce();
            h.PlayerInput.Release();
            for (int i = 0; i < 20; i++)
            {
                h.Tick(1, Dt);
                Assert.AreEqual(CombatState.Running, h.Player.State, $"tick {i}");
                Assert.AreEqual(new Vector2(-1f, 0f), h.Player.Facing, $"tick {i}: faces the run direction");
                Assert.AreEqual(new Vector2(-1f, 0f), h.Cpu.Facing, $"tick {i}: the CPU keeps facing the runner");
            }
            h.PlayerInput.PressRunOnce(); // cancel
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing, "Focus back on when the run ends");
        }

        // COMBAT_SPEC Focus: "A non-running wrestler keeps focusing on a running opponent": the runner passes
        // the CPU (another depth, D-030 no collision); the CPU flips to follow, the runner keeps its run facing.
        [Test]
        public void RunnerPassesOpponent_OpponentTurnsToFollow_RunnerKeepsRunFacing()
        {
            var p = new Vector2(5f, -12f);
            using var h = new MatchHarness(p, new Vector2(12f, p.y - PassGap));
            h.Tick(1, Dt);
            h.PlayerInput.PressRunOnce(); // nothing held: toward the CPU (right)
            h.Tick(1, Dt);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            for (int i = 0; h.Player.State == CombatState.Running; i++)
            {
                Assert.Less(i, 400, "run never ended");
                Assert.AreEqual(1, h.Player.HorizontalFacing, $"tick {i}: runner faces the run direction");
                float dx = h.Player.Position.x - h.Cpu.Position.x;
                if (dx != 0f)
                    Assert.AreEqual(dx > 0f ? 1 : -1, h.Cpu.HorizontalFacing, $"tick {i}: the CPU follows the runner");
                h.Tick(1, Dt);
            }
            Assert.IsEmpty(h.KnockDowns, "no run collision at another depth");
            Assert.Greater(h.Player.Position.x, h.Cpu.Position.x, "precondition: the runner passed the CPU");
            Assert.AreEqual(1, h.Cpu.HorizontalFacing);
            Assert.AreEqual(CombatState.Standing, h.Player.State, "the run ended at the right edge");
            Assert.AreEqual(-1, h.Player.HorizontalFacing, "Focus back on: faces the CPU behind");
        }

        // ================================================================ CPU: same rules

        // CLAUDE.md shared rules / D-037 "This applies to the player and the CPU alike": the CPU's own movement
        // input doesn't turn it, it faces the player, and its Run with nothing held goes toward the player.
        [Test]
        public void Cpu_WalksAway_StillFacesPlayer_AndRunsTowardPlayerWithNothingHeld()
        {
            using var h = new MatchHarness(new Vector2(8f, -12f), new Vector2(14f, -12.6f));
            h.CpuIdle = false;
            h.CpuInput.HeldMove = new Vector2(1f, 1f); // away from the player, toward the trailer
            for (int i = 0; i < 20; i++)
            {
                h.Tick(1, Dt);
                Assert.AreEqual(-1, h.Cpu.HorizontalFacing, $"tick {i}");
            }
            Assert.AreEqual(CombatState.Walking, h.Cpu.State);
            h.CpuInput.Release();
            h.CpuInput.PressRunOnce();
            float x0 = h.Cpu.Position.x;
            h.Tick(5, Dt);
            Assert.AreEqual(CombatState.Running, h.Cpu.State);
            Assert.Less(h.Cpu.Position.x, x0, "CPU run with nothing held goes toward the player");
            Assert.AreEqual(new Vector2(-1f, 0f), h.Cpu.Facing);
        }

        // Shared rules, CPU side of the stance rule: the CPU takes the back stance toward a much higher player.
        [Test]
        public void Cpu_PlayerMuchHigher_CpuBackStance()
        {
            using var h = new MatchHarness(new Vector2(8f, -13.5f), new Vector2(14f, -13.5f));
            float t = Threshold(h);
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickUntil(() => h.Player.Position.y - h.Cpu.Position.y > t + 0.1f, 3f, Dt);
            h.PlayerInput.Release();
            h.Tick(1, Dt);
            Assert.AreEqual(new Vector2(-1f, 1f).normalized, h.Cpu.Facing);
            Assert.IsTrue(h.CpuW.IsBackFacing);
            Assert.AreEqual(new Vector2(1f, 0f), h.Player.Facing, "the higher player stays front");
        }
    }
}
