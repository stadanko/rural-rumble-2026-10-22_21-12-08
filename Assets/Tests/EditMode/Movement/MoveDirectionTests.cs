using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-001 / CONTROLS.md: the stick maps to exactly eight walking directions.
    /// ARCHITECTURE 4.1: Move is raw, Combat quantizes it; the deadzone is a tuning value (moveDeadzone).
    /// </summary>
    public class MoveDirectionTests
    {
        private CombatTuning tuning;

        [SetUp] public void SetUp() => tuning = ScriptableObject.CreateInstance<CombatTuning>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(tuning);

        // CONTROLS.md: Stick Up/Up-Left/Left/Down-Left/Down/Down-Right/Right/Up-Right -> that direction.
        [TestCase(1f, 0f, 1, 0)]
        [TestCase(1f, 1f, 1, 1)]
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(-1f, 1f, -1, 1)]
        [TestCase(-1f, 0f, -1, 0)]
        [TestCase(-1f, -1f, -1, -1)]
        [TestCase(0f, -1f, 0, -1)]
        [TestCase(1f, -1f, 1, -1)]
        public void Quantize_FullStick_SnapsToTheEightDirections(float x, float y, int ex, int ey)
        {
            Assert.AreEqual(new Vector2Int(ex, ey), MoveDirection.Quantize(new Vector2(x, y), tuning.moveDeadzone));
        }

        // D-001 eight directions: each 45-degree sector maps to its nearest direction (boundaries at 22.5 deg).
        [TestCase(20f, 1, 0)]
        [TestCase(25f, 1, 1)]
        [TestCase(65f, 1, 1)]
        [TestCase(70f, 0, 1)]
        [TestCase(160f, -1, 0)]
        [TestCase(-110f, 0, -1)]
        [TestCase(-155f, -1, -1)]
        [TestCase(-160f, -1, 0)]
        public void Quantize_AngledStick_SnapsToNearestSector(float degrees, int ex, int ey)
        {
            float r = degrees * Mathf.Deg2Rad;
            var raw = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 0.8f;
            Assert.AreEqual(new Vector2Int(ex, ey), MoveDirection.Quantize(raw, tuning.moveDeadzone));
        }

        [Test]
        public void Quantize_InsideDeadzone_IsNoDirection()
        {
            float dz = tuning.moveDeadzone;
            Assert.AreEqual(Vector2Int.zero, MoveDirection.Quantize(Vector2.zero, dz));
            Assert.AreEqual(Vector2Int.zero, MoveDirection.Quantize(new Vector2(dz * 0.95f, 0f), dz));
            Assert.AreEqual(Vector2Int.zero, MoveDirection.Quantize(new Vector2(0f, -dz * 0.95f), dz));
            // Radial deadzone: a diagonal whose length is under the deadzone is ignored too.
            Vector2 diag = new Vector2(1f, 1f).normalized * dz * 0.95f;
            Assert.AreEqual(Vector2Int.zero, MoveDirection.Quantize(diag, dz));
        }

        [Test]
        public void Quantize_JustOutsideDeadzone_GivesADirection()
        {
            float dz = tuning.moveDeadzone;
            Assert.AreEqual(new Vector2Int(1, 0), MoveDirection.Quantize(new Vector2(dz * 1.05f, 0f), dz));
            Vector2 diag = new Vector2(-1f, 1f).normalized * dz * 1.05f;
            Assert.AreEqual(new Vector2Int(-1, 1), MoveDirection.Quantize(diag, dz));
        }

        [Test]
        public void Wrestler_StickInsideDeadzone_StaysStanding()
        {
            using var h = new MatchHarness(SliceArena.OpenFloor);
            h.PlayerInput.HeldMove = new Vector2(h.Tuning.moveDeadzone * 0.9f, 0f);
            h.Tick(20);
            Assert.AreEqual(CombatState.Standing, h.Player.State);
            V2Assert.AreEqual(SliceArena.OpenFloor, h.Player.Position, 1e-5f);
        }

        [Test]
        public void Intent_MoveAxesAreClampedToUnitRange()
        {
            // ARCHITECTURE 4.1: Move is each axis -1..1.
            var i = new WrestlerIntent(new Vector2(5f, -3f), ButtonState.None, ButtonState.None, ButtonState.None);
            Assert.AreEqual(new Vector2(1f, -1f), i.Move);
        }
    }
}
