using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 4.2 CombatEvents: StateChanged fires once per transition; FacingChanged fires when the
    /// 8-way Facing changes (since D-037 Focus, from positions, never from movement input). Both fire inside CombatMatch.Tick with the right wrestler id.
    /// </summary>
    public class CombatEventsTests
    {
        [Test]
        public void StateChanged_FiresOncePerRealTransition()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.Tick(10); // standing still: nothing
            Assert.IsEmpty(h.StateChanges);

            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(10);
            CollectionAssert.AreEqual(new[] { (WrestlerId.Player, CombatState.Standing, CombatState.Walking) }, h.StateChanges);

            h.PlayerInput.HeldMove = new Vector2(-1f, 1f); // change direction while walking: still Walking
            h.Tick(10);
            Assert.AreEqual(1, h.StateChanges.Count);

            h.PlayerInput.Release();
            h.Tick(10);
            Assert.AreEqual(2, h.StateChanges.Count);
            Assert.AreEqual((WrestlerId.Player, CombatState.Walking, CombatState.Standing), h.StateChanges[1]);
        }

        [Test]
        public void StateChanged_WalkingIntoWall_DoesNotRepeat()
        {
            using var h = new MatchHarness(new Vector2(1f, -12f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(2f);
            Assert.AreEqual(1, h.StateChanges.Count);
        }

        [Test]
        public void StateChanged_RunToEdge_StandingRunningStanding()
        {
            using var h = new MatchHarness(new Vector2(20f, -12f), cpuStart: new Vector2(26f, -13f));
            h.PlayerInput.PressRunOnce();
            h.TickSeconds(2f);
            CollectionAssert.AreEqual(new[]
            {
                (WrestlerId.Player, CombatState.Standing, CombatState.Running),
                (WrestlerId.Player, CombatState.Running, CombatState.Standing),
            }, h.StateChanges);
        }

        [Test]
        public void StateChanged_FromMatchesPreviousTo()
        {
            using var h = new MatchHarness(new Vector2(20f, -12f));
            h.PlayerInput.HeldMove = Vector2.left; h.Tick(5);
            h.PlayerInput.Release(); h.PlayerInput.PressRunOnce(); h.Tick(5);
            h.TickSeconds(4f);
            h.PlayerInput.HeldMove = Vector2.up; h.Tick(5);
            h.PlayerInput.Release(); h.Tick(5);
            CombatState expectedFrom = CombatState.Standing;
            foreach (var (id, from, to) in h.StateChanges)
            {
                Assert.AreEqual(WrestlerId.Player, id);
                Assert.AreEqual(expectedFrom, from);
                Assert.AreNotEqual(from, to);
                expectedFrom = to;
            }
            Assert.AreEqual(h.Player.State, expectedFrom);
        }

        // Rewritten for D-037 (Focus): facing changes come from positions, not input. Walking either way with
        // the opponent on one side is no change; the opponent rising past the back-stance height is one real
        // change (for the lower wrestler only); standing still is no change.
        [Test]
        public void FacingChanged_FiresOncePerRealChange_WithNewFacing()
        {
            using var h = new MatchHarness(new Vector2(10f, -13.5f), cpuStart: new Vector2(14f, -13.5f));
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(10);
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(10);
            Assert.IsEmpty(h.FacingChanges, "walking left or right with the opponent to the right: no change");

            // The player rises more than the back-stance height above the CPU: the CPU turns to the back stance.
            float threshold = h.Tuning.focusBackStanceHeightPx / 60f;
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickUntil(() => h.Player.Position.y - h.Cpu.Position.y > threshold + 0.1f, 3f);
            h.PlayerInput.Release();
            h.Tick(10);
            CollectionAssert.AreEqual(new[] { (WrestlerId.Cpu, new Vector2(-1f, 1f).normalized) }, h.FacingChanges);

            h.Tick(10);
            Assert.AreEqual(1, h.FacingChanges.Count, "Standing does not change facing");
        }

        // Rewritten for D-037: stick input never turns a wrestler, so neither stick noise nor full swings
        // (left, right, up, down, diagonals) raise FacingChanged while the opponent stays on one side.
        [Test]
        public void FacingChanged_StickNoiseAndSwings_DoNotFire()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            for (int i = 0; i < 20; i++)
            {
                h.PlayerInput.HeldMove = new Vector2(-1f, (i % 2 == 0 ? 0.1f : -0.1f));
                h.Tick(1);
            }
            Vector2[] swings = { Vector2.right, Vector2.up, new Vector2(-1f, 1f), Vector2.down, new Vector2(1f, -1f), Vector2.left };
            foreach (Vector2 m in swings)
            {
                h.PlayerInput.HeldMove = m;
                h.Tick(2);
            }
            Assert.IsEmpty(h.FacingChanges);
        }

        // Rewritten for D-037 (was: run from facing up fires once to horizontal). From the back stance, a run
        // with nothing held goes toward the opponent: exactly one change, to the horizontal run facing. When
        // the run ends past the opponent, Focus turns the wrestler back toward them: one more change.
        [Test]
        public void FacingChanged_RunFromBackStance_FiresOnceToHorizontal_ThenOnceWhenRunEnds()
        {
            float threshold;
            {
                var t = ScriptableObject.CreateInstance<CombatTuning>();
                threshold = t.focusBackStanceHeightPx / 60f;
                Object.DestroyImmediate(t);
            }
            using var h = new MatchHarness(new Vector2(10f, -13.5f), cpuStart: new Vector2(14f, -13.5f + threshold + 0.2f));
            Assert.AreEqual(new Vector2(1f, 1f).normalized, h.Player.Facing, "precondition: back stance");
            h.PlayerInput.PressRunOnce();
            h.Tick(1);
            Assert.AreEqual(CombatState.Running, h.Player.State);
            CollectionAssert.AreEqual(new[] { (WrestlerId.Player, new Vector2(1f, 0f)) },
                h.FacingChanges.Where(e => e.id == WrestlerId.Player).ToArray());

            h.TickUntil(() => h.Player.State != CombatState.Running, 5f);
            CollectionAssert.AreEqual(new[]
            {
                (WrestlerId.Player, new Vector2(1f, 0f)),
                (WrestlerId.Player, new Vector2(-1f, 1f).normalized),
            }, h.FacingChanges.Where(e => e.id == WrestlerId.Player).ToArray());
        }

        [Test]
        public void Events_OnlyNameTheWrestlerThatChanged()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(10);
            Assert.IsTrue(h.StateChanges.All(e => e.id == WrestlerId.Player));
            Assert.IsTrue(h.FacingChanges.All(e => e.id == WrestlerId.Player));
        }
    }
}
