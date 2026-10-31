using System.Linq;
using NUnit.Framework;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 4.2 CombatEvents: StateChanged fires once per transition; FacingChanged fires when the
    /// 8-way Facing changes. Both fire inside CombatMatch.Tick with the right wrestler id.
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

        [Test]
        public void FacingChanged_FiresOncePerRealChange_WithNewFacing()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor); // starts facing right
            h.PlayerInput.HeldMove = Vector2.right;
            h.Tick(10);
            Assert.IsEmpty(h.FacingChanges, "already facing right");

            h.PlayerInput.HeldMove = Vector2.left;
            h.Tick(10);
            Assert.AreEqual(1, h.FacingChanges.Count);
            Assert.AreEqual((WrestlerId.Player, new Vector2(-1f, 0f)), h.FacingChanges[0]);

            h.PlayerInput.HeldMove = new Vector2(-1f, 1f);
            h.Tick(10);
            Assert.AreEqual(2, h.FacingChanges.Count);
            Assert.AreEqual(new Vector2(-1f, 1f).normalized, h.FacingChanges[1].facing);

            h.PlayerInput.Release();
            h.Tick(10);
            Assert.AreEqual(2, h.FacingChanges.Count, "Standing does not change facing");
        }

        [Test]
        public void FacingChanged_StickNoiseWithinSameSector_DoesNotFire()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            for (int i = 0; i < 20; i++)
            {
                h.PlayerInput.HeldMove = new Vector2(-1f, (i % 2 == 0 ? 0.1f : -0.1f));
                h.Tick(1);
            }
            Assert.AreEqual(1, h.FacingChanges.Count);
        }

        [Test]
        public void FacingChanged_RunFromFacingUp_FiresOnceToHorizontal()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = Vector2.up;
            h.Tick(2);
            h.FacingChanges.Clear();
            h.PlayerInput.Release();
            h.PlayerInput.PressRunOnce();
            h.Tick(10);
            CollectionAssert.AreEqual(new[] { (WrestlerId.Player, new Vector2(1f, 0f)) }, h.FacingChanges);
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
