using System.Collections.Generic;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Marks one arena rectangle as data for <see cref="ArenaGeometry"/> (ARCHITECTURE 4.2, 3.1).
    /// The rect is relative to this transform's world position (rotation and scale are ignored), so it
    /// moves with the object it is placed on. Collected once at match start by <see cref="CombatMatchRunner"/>.
    /// Holds no rules: what happens on contact is decided by combat code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaObstacleMarker : MonoBehaviour
    {
        public enum MarkerRole
        {
            /// <summary>The walkable feet band. Its top edge is the back wall. Exactly one per arena.</summary>
            WalkableArea,
            /// <summary>A footprint the feet may not enter (truck, pool).</summary>
            Obstacle
        }

        [SerializeField] private MarkerRole role = MarkerRole.Obstacle;
        [Tooltip("Lower-left corner of the rect relative to this transform's position, world units.")]
        [SerializeField] private Vector2 offset;
        [Tooltip("Width (x) and depth (y) of the rect, world units.")]
        [SerializeField] private Vector2 size = Vector2.one;

        public MarkerRole Role => role;

        public ArenaRect WorldRect
        {
            get
            {
                Vector2 min = (Vector2)transform.position + offset;
                return ArenaRect.FromMinMax(min, min + new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)));
            }
        }

        /// <summary>Configures the marker from code (Editor setup scripts, tests).</summary>
        public void Configure(MarkerRole markerRole, Vector2 localOffset, Vector2 rectSize)
        {
            role = markerRole;
            offset = localOffset;
            size = rectSize;
        }

        /// <summary>
        /// Builds arena geometry from markers. Uses the first WalkableArea marker as the bounds (warns if
        /// there are several) and every Obstacle marker as an obstacle. With no WalkableArea marker the
        /// arena is unbounded and an error is logged.
        /// </summary>
        public static ArenaGeometry BuildGeometry(IEnumerable<ArenaObstacleMarker> markers)
        {
            ArenaRect? bounds = null;
            int walkableCount = 0;
            var obstacles = new List<ArenaRect>();
            foreach (ArenaObstacleMarker m in markers)
            {
                if (m == null || !m.isActiveAndEnabled)
                    continue;
                if (m.role == MarkerRole.WalkableArea)
                {
                    walkableCount++;
                    if (bounds == null)
                        bounds = m.WorldRect;
                }
                else
                {
                    obstacles.Add(m.WorldRect);
                }
            }

            if (walkableCount > 1)
                Debug.LogWarning($"ArenaObstacleMarker: {walkableCount} WalkableArea markers found; using the first.");
            if (bounds == null)
            {
                Debug.LogError("ArenaObstacleMarker: no WalkableArea marker found; wrestlers are unbounded.");
                return new ArenaGeometry(ArenaGeometry.Unbounded().Bounds, obstacles);
            }
            return new ArenaGeometry(bounds.Value, obstacles);
        }

        private void OnDrawGizmos()
        {
            ArenaRect r = WorldRect;
            Gizmos.color = role == MarkerRole.WalkableArea ? new Color(0.2f, 1f, 0.3f, 0.9f) : new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(r.Center.x, r.Center.y, 0f), new Vector3(r.Width, r.Height, 0f));
        }
    }
}
