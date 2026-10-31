using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// ARCHITECTURE 3.1: the truck and pool get colliders at their footprints (rect table).
    /// Reachable faces: the truck's right and front faces and the pool's left and front faces. Their
    /// other sides are the back wall or lie past the band's edge, so feet can't reach them.
    /// </summary>
    public class ObstacleTests
    {
        private const float Contact = 0.01f; // allowed gap between the feet box and a face

        [Test]
        public void Truck_BlocksFromTheRight()
        {
            using var h = new MatchHarness(new Vector2(11f, SliceArena.ObstacleLaneY));
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("walking left into truck"));
            float face = SliceArena.Truck.XMax + h.Tuning.feetHalfWidth;
            Assert.That(h.Player.Position.x, Is.InRange(face, face + Contact));
            Assert.AreEqual(SliceArena.ObstacleLaneY, h.Player.Position.y, 1e-4f);
        }

        [Test]
        public void Truck_BlocksFromTheFront()
        {
            using var h = new MatchHarness(new Vector2(4f, -12.5f));
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("walking up into truck"));
            float face = SliceArena.Truck.YMin - h.Tuning.feetHalfDepth;
            Assert.That(h.Player.Position.y, Is.InRange(face - Contact, face));
        }

        [Test]
        public void Pool_BlocksFromTheLeft()
        {
            using var h = new MatchHarness(new Vector2(15f, SliceArena.ObstacleLaneY));
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("walking right into pool"));
            float face = SliceArena.Pool.XMin - h.Tuning.feetHalfWidth;
            Assert.That(h.Player.Position.x, Is.InRange(face - Contact, face));
        }

        [Test]
        public void Pool_BlocksFromTheFront()
        {
            using var h = new MatchHarness(new Vector2(22f, -12.5f));
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("walking up into pool"));
            float face = SliceArena.Pool.YMin - h.Tuning.feetHalfDepth;
            Assert.That(h.Player.Position.y, Is.InRange(face - Contact, face));
        }

        // Sliding: walking along a face after being stopped by it must not stick.
        [Test]
        public void Truck_SlideAlongFrontFace()
        {
            using var h = new MatchHarness(new Vector2(4f, -12.5f));
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(2f);
            float y = h.Player.Position.y;
            h.PlayerInput.HeldMove = Vector2.left;
            h.TickSeconds(0.5f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("sliding left under truck"));
            Assert.AreEqual(4f - h.Tuning.walkSpeedHorizontal * 0.5f, h.Player.Position.x, 0.05f, "did not slide along the truck's front face");
            Assert.AreEqual(y, h.Player.Position.y, 1e-4f);
        }

        [Test]
        public void Truck_DiagonalIntoFrontFace_SlidesHorizontally()
        {
            using var h = new MatchHarness(new Vector2(6f, -11.5f));
            h.PlayerInput.HeldMove = new Vector2(-1f, 1f); // up-left into the truck's front
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("up-left under truck"));
            Assert.AreEqual(SliceArena.BandXMin + h.Tuning.feetHalfWidth, h.Player.Position.x, 1e-3f, "snagged before reaching the left edge");
            Assert.That(h.Player.Position.y, Is.InRange(SliceArena.Truck.YMin - h.Tuning.feetHalfDepth - Contact, SliceArena.Truck.YMin - h.Tuning.feetHalfDepth));
        }

        [Test]
        public void Truck_DiagonalAlongRightFace_SlidesPastTheCorner()
        {
            using var h = new MatchHarness(new Vector2(8.5f, SliceArena.ObstacleLaneY));
            h.PlayerInput.HeldMove = new Vector2(-1f, -1f); // down-left along the truck's right face
            h.TickSeconds(1.5f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("down-left past truck corner"));
            Assert.That(h.Player.Position.x, Is.LessThan(SliceArena.Truck.XMax), "snagged on the truck's front-right corner");
            Assert.That(h.Player.Position.y, Is.LessThan(SliceArena.Truck.YMin));
        }

        [Test]
        public void Truck_DiagonalStraightAtCorner_NeverEntersAndKeepsMoving()
        {
            using var h = new MatchHarness(new Vector2(9.2f, -11.5f));
            h.PlayerInput.HeldMove = new Vector2(-1f, 1f);
            h.TickSeconds(4f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("up-left at truck corner"));
            // Either slid under the truck to the left edge, or up its right face to the back wall.
            bool slidUnder = Mathf.Abs(h.Player.Position.x - (SliceArena.BandXMin + h.Tuning.feetHalfWidth)) < 1e-3f;
            bool slidUp = Mathf.Abs(h.Player.Position.y - (SliceArena.BandYMax - h.Tuning.feetHalfDepth)) < 1e-3f;
            Assert.IsTrue(slidUnder || slidUp, $"snagged at the corner, stopped at {h.Player.Position}");
        }

        [Test]
        public void Pool_DiagonalIntoFrontFace_SlidesHorizontally()
        {
            using var h = new MatchHarness(new Vector2(20f, -11.5f), cpuStart: new Vector2(1f, -13f));
            h.PlayerInput.HeldMove = new Vector2(1f, 1f);
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("up-right under pool"));
            Assert.AreEqual(SliceArena.BandXMax - h.Tuning.feetHalfWidth, h.Player.Position.x, 1e-3f);
        }

        [Test]
        public void Pool_DiagonalAlongLeftFace_SlidesPastTheCorner()
        {
            using var h = new MatchHarness(new Vector2(17.5f, SliceArena.ObstacleLaneY));
            h.PlayerInput.HeldMove = new Vector2(1f, -1f);
            h.TickSeconds(1.5f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("down-right past pool corner"));
            Assert.That(h.Player.Position.x, Is.GreaterThan(SliceArena.Pool.XMin));
        }

        // Same rules with a non-zero feet depth (tuning value, D-005).
        [Test]
        public void Obstacles_WithFeetDepth_StillBlockAndSlide()
        {
            using var h = new MatchHarness(new Vector2(6f, -12f), configure: t => t.feetHalfDepth = 0.25f);
            h.PlayerInput.HeldMove = new Vector2(-1f, 1f);
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("feet depth 0.25 under truck"));
            Assert.AreEqual(SliceArena.Truck.YMin - 0.25f, h.Player.Position.y, Contact);
            h.PlayerInput.HeldMove = Vector2.right;
            h.TickSeconds(3f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("feet depth 0.25 sliding right"));
            Assert.That(h.Player.Position.x, Is.GreaterThan(SliceArena.Truck.XMax + 0.5f));
            h.PlayerInput.HeldMove = Vector2.up;
            h.TickSeconds(2f, afterEachTick: _ => h.AssertPlayerOutsideObstacles("feet depth 0.25 to back wall"));
            Assert.AreEqual(SliceArena.BandYMax - 0.25f, h.Player.Position.y, 1e-3f);
        }

        // ArenaGeometry directly: a sweep reports obstacle vs edge so combat can tell them apart.
        [Test]
        public void Geometry_ReportsObstacleVersusEdge()
        {
            ArenaGeometry arena = SliceArena.Build();
            var half = new Vector2(0.5f, 0f);
            ArenaMoveResult intoTruck = arena.Move(new Vector2(9f, SliceArena.ObstacleLaneY), new Vector2(-5f, 0f), half);
            Assert.IsTrue(intoTruck.BlockedX);
            Assert.IsTrue(intoTruck.HitObstacleX);
            ArenaMoveResult intoEdge = arena.Move(new Vector2(3f, -12f), new Vector2(-5f, 0f), half);
            Assert.IsTrue(intoEdge.BlockedX);
            Assert.IsFalse(intoEdge.HitObstacleX);
            ArenaMoveResult free = arena.Move(SliceArena.OpenFloor, new Vector2(1f, 0.5f), half);
            Assert.IsFalse(free.BlockedX || free.BlockedY);
        }
    }
}
