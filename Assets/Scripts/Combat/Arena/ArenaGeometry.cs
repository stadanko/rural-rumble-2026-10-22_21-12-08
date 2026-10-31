using System.Collections.Generic;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>Outcome of moving a feet box through the arena for one tick.</summary>
    public readonly struct ArenaMoveResult
    {
        public readonly Vector2 Position;
        /// <summary>Horizontal motion was cut short by the walkable-area edge or an obstacle.</summary>
        public readonly bool BlockedX;
        /// <summary>Depth motion was cut short by the walkable-area edge (incl. the back wall) or an obstacle.</summary>
        public readonly bool BlockedY;
        /// <summary>The horizontal block was an obstacle rect (truck, pool), not the arena edge.</summary>
        public readonly bool HitObstacleX;
        /// <summary>The depth block was an obstacle rect, not the arena edge.</summary>
        public readonly bool HitObstacleY;

        public ArenaMoveResult(Vector2 position, bool blockedX, bool blockedY, bool hitObstacleX, bool hitObstacleY)
        {
            Position = position;
            BlockedX = blockedX;
            BlockedY = blockedY;
            HitObstacleX = hitObstacleX;
            HitObstacleY = hitObstacleY;
        }
    }

    /// <summary>
    /// Walkable band plus obstacle footprints on the arena plane (ARCHITECTURE 4.2 hit detection,
    /// 3.1 rectangles). Plain C#: built from <see cref="ArenaObstacleMarker"/>s at match start, or
    /// directly by tests. A wrestler is a feet box (center = feet point) that must stay inside the
    /// walkable bounds and out of every obstacle. The back wall is the bounds' top edge.
    /// </summary>
    public sealed class ArenaGeometry
    {
        /// <summary>Gap left between a blocked feet box and the obstacle face, so sliding along it works.</summary>
        public const float Skin = 0.002f;
        /// <summary>Tolerance when deciding whether a feet box lines up with an obstacle (float noise between markers).</summary>
        public const float Tolerance = 0.0005f;

        private readonly List<ArenaRect> obstacles;

        public ArenaRect Bounds { get; }
        public IReadOnlyList<ArenaRect> Obstacles => obstacles;

        public ArenaGeometry(ArenaRect walkableBounds, IEnumerable<ArenaRect> obstacleRects)
        {
            Bounds = walkableBounds;
            obstacles = obstacleRects != null ? new List<ArenaRect>(obstacleRects) : new List<ArenaRect>();
        }

        /// <summary>No edges and no obstacles. Used when the scene has no walkable-area marker.</summary>
        public static ArenaGeometry Unbounded() =>
            new ArenaGeometry(new ArenaRect(-1e6f, -1e6f, 1e6f, 1e6f), null);

        /// <summary>Clamps a feet point into the walkable bounds (does not push out of obstacles).</summary>
        public Vector2 ClampToBounds(Vector2 feet, Vector2 halfExtents)
        {
            return new Vector2(
                ClampAxis(feet.x, Bounds.XMin + halfExtents.x, Bounds.XMax - halfExtents.x),
                ClampAxis(feet.y, Bounds.YMin + halfExtents.y, Bounds.YMax - halfExtents.y));
        }

        /// <summary>
        /// Moves a feet box by <paramref name="delta"/>: horizontal first, then depth, each swept against
        /// obstacles (no tunneling at large dt) and clamped to the bounds. A box that already overlaps an
        /// obstacle (bad spawn) is allowed to move out.
        /// </summary>
        public ArenaMoveResult Move(Vector2 from, Vector2 delta, Vector2 halfExtents)
        {
            float hw = halfExtents.x;
            float hd = halfExtents.y;

            // Horizontal.
            float x0 = from.x;
            float x1 = from.x + delta.x;
            float y = from.y;
            bool blockedX = false;
            bool hitObstacleX = false;
            if (delta.x != 0f)
            {
                for (int i = 0; i < obstacles.Count; i++)
                {
                    ArenaRect o = obstacles[i];
                    if (!OverlapsClosed(y - hd, y + hd, o.YMin, o.YMax))
                        continue;
                    if (delta.x > 0f && x0 + hw <= o.XMin + Tolerance && x1 + hw > o.XMin - Skin)
                    {
                        x1 = Mathf.Max(x0, o.XMin - hw - Skin);
                        blockedX = hitObstacleX = true;
                    }
                    else if (delta.x < 0f && x0 - hw >= o.XMax - Tolerance && x1 - hw < o.XMax + Skin)
                    {
                        x1 = Mathf.Min(x0, o.XMax + hw + Skin);
                        blockedX = hitObstacleX = true;
                    }
                }
            }
            float minX = Bounds.XMin + hw;
            float maxX = Bounds.XMax - hw;
            if (x1 < minX) { if (delta.x < 0f) { blockedX = true; hitObstacleX = false; } x1 = minX; }
            if (x1 > maxX) { if (delta.x > 0f) { blockedX = true; hitObstacleX = false; } x1 = maxX; }
            if (minX > maxX) x1 = Bounds.Center.x;

            // Depth.
            float x = x1;
            float y0 = from.y;
            float y1 = from.y + delta.y;
            bool blockedY = false;
            bool hitObstacleY = false;
            if (delta.y != 0f)
            {
                for (int i = 0; i < obstacles.Count; i++)
                {
                    ArenaRect o = obstacles[i];
                    if (!OverlapsClosed(x - hw, x + hw, o.XMin, o.XMax))
                        continue;
                    if (delta.y > 0f && y0 + hd <= o.YMin + Tolerance && y1 + hd > o.YMin - Skin)
                    {
                        y1 = Mathf.Max(y0, o.YMin - hd - Skin);
                        blockedY = hitObstacleY = true;
                    }
                    else if (delta.y < 0f && y0 - hd >= o.YMax - Tolerance && y1 - hd < o.YMax + Skin)
                    {
                        y1 = Mathf.Min(y0, o.YMax + hd + Skin);
                        blockedY = hitObstacleY = true;
                    }
                }
            }
            float minY = Bounds.YMin + hd;
            float maxY = Bounds.YMax - hd;
            if (y1 < minY) { if (delta.y < 0f) { blockedY = true; hitObstacleY = false; } y1 = minY; }
            if (y1 > maxY) { if (delta.y > 0f) { blockedY = true; hitObstacleY = false; } y1 = maxY; }
            if (minY > maxY) y1 = Bounds.Center.y;

            return new ArenaMoveResult(new Vector2(x1, y1), blockedX, blockedY, hitObstacleX, hitObstacleY);
        }

        private static bool OverlapsClosed(float aMin, float aMax, float bMin, float bMax) =>
            aMax >= bMin - Tolerance && aMin <= bMax + Tolerance;

        private static float ClampAxis(float v, float min, float max) =>
            min <= max ? Mathf.Clamp(v, min, max) : (min + max) * 0.5f;
    }
}
