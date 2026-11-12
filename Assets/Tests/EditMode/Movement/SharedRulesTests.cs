using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// CLAUDE.md "All character input sources use shared combat logic"; ARCHITECTURE 4.1 (CPU intents go
    /// through the same rules); the idle stand-in opponent never acts; D-005 tuning edits apply on the
    /// next use (ARCHITECTURE 5: rules read fields at the moment of use).
    /// </summary>
    public class SharedRulesTests
    {
        [Test]
        public void IdleInput_ReturnsNoMoveAndNoButtons()
        {
            for (int i = 0; i < 5; i++)
            {
                WrestlerIntent intent = IdleInput.Instance.Read();
                Assert.AreEqual(Vector2.zero, intent.Move);
                Assert.IsFalse(intent.RunWhip.Pressed || intent.RunWhip.Held);
                Assert.IsFalse(intent.Strike.Pressed || intent.Strike.Held);
                Assert.IsFalse(intent.LockUpGrapple.Pressed || intent.LockUpGrapple.Held);
            }
        }

        [Test]
        public void IdleCpu_NeverMovesOrChangesState_WhilePlayerActs()
        {
            // CPU off the player's depth line: since D-027 wrestlers block each other, and a run into the
            // opponent knocks both down (D-029). (Updated in combat-core step 2; the movement slice let the
            // player walk through the CPU.)
            var cpuStart = new Vector2(17.6667f, -13.5f);
            using var h = new MatchHarness(new Vector2(9f, -11.6667f), cpuStart: cpuStart);
            // Player walks past the CPU and runs across the arena. Since D-037 the idle CPU's facing follows the
            // player (Focus), but it still never moves or changes state.
            float threshold = h.Tuning.focusBackStanceHeightPx / 60f;
            void AssertCpuFacesPlayer(string when)
            {
                float dx = h.Player.Position.x - h.Cpu.Position.x;
                if (dx == 0f) return;
                bool back = h.Player.Position.y - h.Cpu.Position.y > threshold;
                Vector2 expected = new Vector2(dx > 0f ? 1f : -1f, back ? 1f : 0f).normalized;
                Assert.AreEqual(expected, h.Cpu.Facing, when);
            }
            h.PlayerInput.HeldMove = Vector2.right; h.TickSeconds(3f, afterEachTick: i => AssertCpuFacesPlayer($"walk {i}"));
            h.PlayerInput.Release(); h.PlayerInput.PressRunOnce(); h.TickSeconds(3f, afterEachTick: i => AssertCpuFacesPlayer($"run {i}"));
            h.PlayerInput.HeldMove = new Vector2(-1f, 1f); h.TickSeconds(3f, afterEachTick: i => AssertCpuFacesPlayer($"walk back {i}"));
            V2Assert.AreEqual(cpuStart, h.Cpu.Position, 1e-6f);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.IsFalse(h.StateChanges.Any(e => e.id == WrestlerId.Cpu));
            Assert.IsTrue(h.FacingChanges.Any(e => e.id == WrestlerId.Cpu), "precondition: the player crossed the CPU");
        }

        // Shared rules: the CPU's intents go through exactly the same movement rules as the player's.
        // Updated for D-037: facing now depends on which side the opponent is on, so identical intents no
        // longer give identical facing. The wrestlers stand mirrored and get mirrored intents (walk away, walk
        // diagonally away and down, then Run with nothing held = toward each other, at another depth): states
        // must match, facings and x travel must mirror, and depth travel must match.
        [Test]
        public void CpuAndPlayer_MirroredIntents_MirroredResults()
        {
            var playerStart = new Vector2(8f, -13f);
            // Different depths, but well under the back-stance height (default 50 px = 0.83 units), so both start
            // in front stance: 0.4 units = 24 px.
            var cpuStart = new Vector2(18f, -12.6f);
            using var h = new MatchHarness(playerStart, cpuStart: cpuStart);
            Assert.That(cpuStart.y - playerStart.y, Is.LessThan(h.Tuning.focusBackStanceHeightPx / 60f), "precondition: both front stance");
            h.CpuIdle = false;
            void Both(Vector2 playerMove) { h.PlayerInput.HeldMove = playerMove; h.CpuInput.HeldMove = new Vector2(-playerMove.x, playerMove.y); }
            void AssertMirrored(string when)
            {
                Assert.AreEqual(h.Player.State, h.Cpu.State, when);
                Assert.AreEqual(-h.Player.HorizontalFacing, h.Cpu.HorizontalFacing, when);
                Assert.AreEqual(new Vector2(-h.Player.Facing.x, h.Player.Facing.y), h.Cpu.Facing, when);
                Assert.AreEqual(h.Player.Position.x - playerStart.x, -(h.Cpu.Position.x - cpuStart.x), 1e-4f, when);
                Assert.AreEqual(h.Player.Position.y - playerStart.y, h.Cpu.Position.y - cpuStart.y, 1e-4f, when);
            }
            Both(Vector2.left); h.Tick(20, afterEachTick: i => AssertMirrored($"walk away {i}"));
            Assert.AreEqual(1, h.Player.HorizontalFacing, "backing away, facing the CPU");
            Both(new Vector2(-1f, -1f)); h.Tick(10, afterEachTick: i => AssertMirrored($"diagonal {i}"));
            Both(Vector2.zero); h.PlayerInput.PressRunOnce(); h.CpuInput.PressRunOnce();
            h.Tick(15, afterEachTick: i => AssertMirrored($"run {i}"));
            Assert.AreEqual(CombatState.Running, h.Player.State);
            Assert.AreEqual(1, h.Player.HorizontalFacing, "nothing held: each runs toward the other");
        }

        [Test]
        public void Inputs_AreReadOncePerTick()
        {
            // ARCHITECTURE 4.1: Combat calls Read() exactly once per combat tick (the harness does the Read,
            // mirroring CombatMatchRunner.Update).
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.CpuIdle = false;
            h.Tick(7);
            Assert.AreEqual(7, h.PlayerInput.ReadCount);
            Assert.AreEqual(7, h.CpuInput.ReadCount);
        }

        [Test]
        public void Match_NullArena_IsUnbounded()
        {
            var tuning = ScriptableObject.CreateInstance<CombatTuning>();
            try
            {
                var match = new CombatMatch(tuning, null, new Vector2(-50f, 50f), new Vector2(0f, 0f));
                var left = new WrestlerIntent(Vector2.left, ButtonState.None, ButtonState.None, ButtonState.None);
                for (int i = 0; i < 50; i++) match.Tick(0.02f, left, WrestlerIntent.None);
                Assert.Less(match.Player.Position.x, -50f);
            }
            finally { Object.DestroyImmediate(tuning); }
        }

        // D-005: tuning edits take effect on the next tick, without rebuilding the match.
        [Test]
        public void TuningEdit_WalkSpeed_AppliesNextTick()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(1);
            float x = h.Player.Position.x;
            h.Tuning.walkSpeedHorizontal = 6f;
            h.Tick(1);
            Assert.AreEqual(x + 6f * MatchHarness.Dt, h.Player.Position.x, 1e-4f);
            h.PlayerInput.HeldMove = Vector2.up;
            h.Tuning.walkSpeedDepth = 0.5f;
            float y = h.Player.Position.y;
            h.Tick(1);
            Assert.AreEqual(y + 0.5f * MatchHarness.Dt, h.Player.Position.y, 1e-4f);
        }

        [Test]
        public void TuningEdit_RunSpeed_AppliesMidRun()
        {
            using var h = new MatchHarness(new Vector2(3f, -12f));
            h.PlayerInput.PressRunOnce();
            h.Tick(2);
            float x = h.Player.Position.x;
            h.Tuning.runSpeed = 12f;
            h.Tick(1);
            Assert.AreEqual(x + 12f * MatchHarness.Dt, h.Player.Position.x, 1e-4f);
        }

        [Test]
        public void TuningEdit_Deadzone_AppliesNextTick()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(0.4f, 0f);
            h.Tuning.moveDeadzone = 0.3f;
            h.Tick(1);
            Assert.AreEqual(CombatState.Walking, h.Player.State);
            h.Tuning.moveDeadzone = 0.5f;
            h.Tick(1);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
        }

        [Test]
        public void TuningEdit_FeetHalfWidth_AppliesNextTick()
        {
            using var h = new MatchHarness(new Vector2(2f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(2f);
            Assert.AreEqual(0.5f, h.Player.Position.x, 1e-3f);
            h.Tuning.feetHalfWidth = 1.0f;
            h.Tick(1);
            Assert.AreEqual(1.0f, h.Player.Position.x, 1e-3f);
        }

        [Test]
        public void TuningEdit_MaxTickStep_AppliesNextTick()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.Tuning.maxTickStep = 0.01f;
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(1, dt: 1f);
            Assert.AreEqual(SliceArena.OpenFloor.x + h.Tuning.walkSpeedHorizontal * 0.01f, h.Player.Position.x, 1e-4f);
        }
    }
}
