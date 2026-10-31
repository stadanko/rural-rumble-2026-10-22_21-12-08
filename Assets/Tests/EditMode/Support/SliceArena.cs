using RuralRumble.Combat;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// The arena rectangles exactly as written in ARCHITECTURE 3.1 "Arena rectangles" (world units),
    /// typed in from the document rather than read from the prefab, so the tests check the code
    /// against the spec. The PlayMode tests check the prefab against the same table.
    /// </summary>
    public static class SliceArena
    {
        public const float BandXMin = 0f;
        public const float BandXMax = 26.6667f;
        public const float BandYMin = -13.6667f; // front, canvas row 820
        public const float BandYMax = -9.6667f;  // back wall (trailer), canvas row 580

        public static readonly ArenaRect Band = new ArenaRect(BandXMin, BandYMin, BandXMax, BandYMax);
        public static readonly ArenaRect Truck = new ArenaRect(-0.6667f, -10.8333f, 7.7f, -9.6667f);
        public static readonly ArenaRect Pool = new ArenaRect(18.3333f, -10.6667f, 27.6667f, -9.6667f);

        /// <summary>Open floor away from both obstacles.</summary>
        public static readonly Vector2 OpenFloor = new Vector2(13f, -12f);

        /// <summary>A depth that lines up with both obstacles' footprints (between back wall and pool front).</summary>
        public const float ObstacleLaneY = -10.2f;

        public static ArenaGeometry Build() => new ArenaGeometry(Band, new[] { Truck, Pool });

        public static ArenaGeometry BandOnly() => new ArenaGeometry(Band, null);

        /// <summary>True when the feet box overlaps the interior of an obstacle (touching a face is fine).</summary>
        public static bool FeetInsideObstacle(Vector2 feet, Vector2 halfExtents, ArenaRect o, float eps = 1e-4f)
        {
            return feet.x + halfExtents.x > o.XMin + eps && feet.x - halfExtents.x < o.XMax - eps
                && feet.y + halfExtents.y > o.YMin + eps && feet.y - halfExtents.y < o.YMax - eps;
        }
    }
}
