using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Keeps the two wrestlers' feet boxes from overlapping (D-027, ARCHITECTURE 4.2 "Wrestler blocking").
    /// No pushing: a wrestler is only ever stopped short along its own path, never moved by the other.
    ///
    /// Order (symmetric, so neither wrestler is favored):
    /// <list type="number">
    /// <item>Each wrestler's move (already resolved against the arena) is swept against the other
    /// wrestler's box at its position from the start of the tick, horizontal first, then depth. Both
    /// sweeps use start-of-tick positions, so the result is the same whichever is computed first.</item>
    /// <item>If the two results still overlap (both stepped into the same gap), the overlap is taken back
    /// along the axis they were separated on at the start, split equally between them. A wrestler never
    /// gives back more than it moved toward the other; any rest is taken from the other.</item>
    /// </list>
    /// If the boxes already overlap at the start of the tick (spawn or a tuning change), nothing is
    /// blocked that tick, so they can separate.
    /// </summary>
    public static class WrestlerBlocking
    {
        private const float Skin = ArenaGeometry.Skin;
        private const float Tol = ArenaGeometry.Tolerance;

        public readonly struct Result
        {
            public readonly Vector2 A;
            public readonly Vector2 B;
            /// <summary>A's horizontal move was cut short by B.</summary>
            public readonly bool ABlockedX;
            public readonly bool ABlockedY;
            public readonly bool BBlockedX;
            public readonly bool BBlockedY;

            public Result(Vector2 a, Vector2 b, bool aX, bool aY, bool bX, bool bY)
            {
                A = a; B = b; ABlockedX = aX; ABlockedY = aY; BBlockedX = bX; BBlockedY = bY;
            }
        }

        /// <summary>True when two feet boxes with these half extents overlap (touching is not overlapping).</summary>
        public static bool Overlaps(Vector2 a, Vector2 b, Vector2 halfExtents) =>
            Overlap(a, b, 2f * halfExtents.x, 2f * halfExtents.y);

        public static Result Resolve(Vector2 aStart, Vector2 aEnd, Vector2 bStart, Vector2 bEnd, Vector2 halfExtents)
        {
            float sx = 2f * halfExtents.x;
            float sy = 2f * halfExtents.y;
            if (Overlap(aStart, bStart, sx, sy))
                return new Result(aEnd, bEnd, false, false, false, false);

            // 1. Each against the other's start box.
            Vector2 a = Sweep(aStart, aEnd, bStart, sx, sy, out bool aX, out bool aY);
            Vector2 b = Sweep(bStart, bEnd, aStart, sx, sy, out bool bX, out bool bY);

            // 2. Both moved into the same gap.
            if (Overlap(a, b, sx, sy))
            {
                bool separatedOnX = Mathf.Abs(aStart.x - bStart.x) >= sx - Tol;
                if (separatedOnX)
                {
                    SplitBack(ref a.x, aStart.x, ref b.x, bStart.x, sx, out bool ga, out bool gb);
                    aX |= ga; bX |= gb;
                }
                else
                {
                    SplitBack(ref a.y, aStart.y, ref b.y, bStart.y, sy, out bool ga, out bool gb);
                    aY |= ga; bY |= gb;
                }
            }
            return new Result(a, b, aX, aY, bX, bY);
        }

        private static void SplitBack(ref float a, float aStart, ref float b, float bStart, float separation,
            out bool aGaveBack, out bool bGaveBack)
        {
            float s = Mathf.Sign(bStart - aStart);          // direction from A toward B on this axis
            if (s == 0f) s = 1f;
            float penetration = separation + Skin - s * (b - a);
            float aToward = Mathf.Max(0f, s * (a - aStart));
            float bToward = Mathf.Max(0f, -s * (b - bStart));
            // Never take back more than they moved together (keeps the result at or past touching).
            penetration = Mathf.Min(penetration, aToward + bToward);

            float half = penetration * 0.5f;
            float aShare = Mathf.Min(aToward, half);
            float bShare = Mathf.Min(bToward, penetration - aShare);
            aShare = Mathf.Min(aToward, penetration - bShare);

            a -= s * aShare;
            b += s * bShare;
            aGaveBack = aShare > 0f;
            bGaveBack = bShare > 0f;
        }

        private static Vector2 Sweep(Vector2 from, Vector2 to, Vector2 box, float sx, float sy, out bool blockedX, out bool blockedY)
        {
            blockedX = blockedY = false;

            // Horizontal, at the starting depth.
            if (to.x != from.x && Mathf.Abs(from.y - box.y) < sy - Tol)
            {
                if (to.x > from.x && from.x <= box.x - sx + Tol && to.x > box.x - sx - Skin)
                {
                    to.x = Mathf.Max(from.x, box.x - sx - Skin);
                    blockedX = true;
                }
                else if (to.x < from.x && from.x >= box.x + sx - Tol && to.x < box.x + sx + Skin)
                {
                    to.x = Mathf.Min(from.x, box.x + sx + Skin);
                    blockedX = true;
                }
            }

            // Depth, at the new horizontal position.
            if (to.y != from.y && Mathf.Abs(to.x - box.x) < sx - Tol)
            {
                if (to.y > from.y && from.y <= box.y - sy + Tol && to.y > box.y - sy - Skin)
                {
                    to.y = Mathf.Max(from.y, box.y - sy - Skin);
                    blockedY = true;
                }
                else if (to.y < from.y && from.y >= box.y + sy - Tol && to.y < box.y + sy + Skin)
                {
                    to.y = Mathf.Min(from.y, box.y + sy + Skin);
                    blockedY = true;
                }
            }
            return to;
        }

        private static bool Overlap(Vector2 a, Vector2 b, float sx, float sy) =>
            Mathf.Abs(a.x - b.x) < sx - Tol && Mathf.Abs(a.y - b.y) < sy - Tol;
    }
}
