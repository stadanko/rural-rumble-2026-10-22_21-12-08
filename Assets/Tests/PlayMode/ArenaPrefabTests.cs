using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using UnityEngine;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// ARCHITECTURE 3.1 "Arena rectangles": ArenaObstacleMarkers on the real Arena.prefab produce the
    /// walkable band and the truck/pool footprints in the table.
    /// </summary>
    public class ArenaPrefabTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void ArenaPrefab_MarkersBuildTheDocumentedRects()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Arena.prefab");
            Assert.IsNotNull(prefab, "Assets/Prefabs/Arena/Arena.prefab not found");
            GameObject instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                ArenaObstacleMarker[] markers = instance.GetComponentsInChildren<ArenaObstacleMarker>();
                Assert.AreEqual(1, markers.Count(m => m.Role == ArenaObstacleMarker.MarkerRole.WalkableArea), "exactly one WalkableArea marker");
                Assert.AreEqual(2, markers.Count(m => m.Role == ArenaObstacleMarker.MarkerRole.Obstacle), "truck + pool obstacles");
                AssertMatchesTable(ArenaObstacleMarker.BuildGeometry(markers));
            }
            finally
            {
                Object.Destroy(instance);
            }
#else
            Assert.Ignore("Needs the Editor AssetDatabase.");
#endif
        }

        /// <summary>Values typed in from the ARCHITECTURE 3.1 table (world units).</summary>
        public static void AssertMatchesTable(ArenaGeometry g)
        {
            AssertRect("walkable band", g.Bounds, 1.5f, -13.6667f, 25.1667f, -9.6667f); // D-027 narrowed band (ARCHITECTURE 3.1)
            Assert.AreEqual(2, g.Obstacles.Count);
            ArenaRect truck = g.Obstacles.OrderBy(o => o.XMin).First();
            ArenaRect pool = g.Obstacles.OrderBy(o => o.XMin).Last();
            AssertRect("truck footprint", truck, -2f, -13f, 6.3667f, -11.8333f); // D-035: designer position (-2, -13)
            AssertRect("pool footprint", pool, 21f, -14f, 30.3333f, -13f); // D-035: designer position (21, -14)
        }

        private static void AssertRect(string name, ArenaRect r, float xMin, float yMin, float xMax, float yMax)
        {
            Assert.AreEqual(xMin, r.XMin, Tol, $"{name} xMin ({r})");
            Assert.AreEqual(yMin, r.YMin, Tol, $"{name} yMin ({r})");
            Assert.AreEqual(xMax, r.XMax, Tol, $"{name} xMax ({r})");
            Assert.AreEqual(yMax, r.YMax, Tol, $"{name} yMax ({r})");
        }
    }
}
