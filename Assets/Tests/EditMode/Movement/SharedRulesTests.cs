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
            var cpuStart = new Vector2(17.6667f, -11.6667f);
            using var h = new MatchHarness(new Vector2(9f, -11.6667f), cpuStart: cpuStart);
            Vector2 cpuFacing = h.Cpu.Facing;
            // Player walks through the CPU's spot and runs across the arena (wrestlers pass through each other).
            h.PlayerInput.HeldMove = Vector2.right; h.TickSeconds(3f);
            h.PlayerInput.Release(); h.PlayerInput.PressRunOnce(); h.TickSeconds(3f);
            h.PlayerInput.HeldMove = new Vector2(-1f, 1f); h.TickSeconds(3f);
            V2Assert.AreEqual(cpuStart, h.Cpu.Position, 1e-6f);
            Assert.AreEqual(CombatState.Standing, h.Cpu.State);
            Assert.AreEqual(cpuFacing, h.Cpu.Facing);
            Assert.IsFalse(h.StateChanges.Any(e => e.id == WrestlerId.Cpu));
            Assert.IsFalse(h.FacingChanges.Any(e => e.id == WrestlerId.Cpu));
        }

        // Shared rules: the CPU's intents go through exactly the same movement rules as the player's.
        [Test]
        public void CpuAndPlayer_SameIntents_SameResults()
        {
            var playerStart = new Vector2(5f, -13f);
            var cpuStart = new Vector2(5f, -11.5f);
            using var h = new MatchHarness(playerStart, cpuStart: cpuStart);
            h.CpuIdle = false;
            // They start facing opposite ways (each faces the other); walking right aligns them.
            void Both(Vector2 move) { h.PlayerInput.HeldMove = move; h.CpuInput.HeldMove = move; }
            Both(Vector2.right); h.Tick(20);
            Both(new Vector2(1f, -1f)); h.Tick(10);
            Both(Vector2.zero); h.PlayerInput.PressRunOnce(); h.CpuInput.PressRunOnce(); h.Tick(15);
            Assert.AreEqual(h.Player.State, h.Cpu.State);
            Assert.AreEqual(h.Player.Facing, h.Cpu.Facing);
            Assert.AreEqual(h.Player.HorizontalFacing, h.Cpu.HorizontalFacing);
            Assert.AreEqual(h.Player.Position.x - playerStart.x, h.Cpu.Position.x - cpuStart.x, 1e-4f);
            // Depth: player is clamped at the front edge, CPU is not, so compare only horizontal travel and state.
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
