using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
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

        // D-047: a run into the depth the enlarged footprints added (behind the old back edges, and the truck's new
        // front strip) crashes: Struck + one ObstacleHit, stopped at the obstacle face. Rows just outside still pass.
        [TestCase(9f, -11.5f, 2f, -10f, -1, TestName = "Truck_RunIntoAddedBackDepth_Crashes")]
        [TestCase(9f, -13.1f, 2f, -13.5f, -1, TestName = "Truck_RunIntoAddedFrontStrip_Crashes")]
        [TestCase(17f, -12f, 24f, -10f, 1, TestName = "Pool_RunIntoAddedBackDepth_Crashes")]
        public void ArenaPrefab_RunIntoEnlargedFootprint_Crashes(float px, float py, float cx, float cy, int dir)
        {
            using (var run = new PrefabArenaRun(new Vector2(px, py), new Vector2(cx, cy)))
            {
                run.RunUntil(() => run.Match.Player.State == CombatState.Struck);
                Assert.AreEqual(1, run.ObstacleHits, "one ObstacleHit for the crash");
                ArenaRect o = dir < 0 ? run.Match.Arena.Obstacles.OrderBy(r => r.XMin).First() : run.Match.Arena.Obstacles.OrderBy(r => r.XMin).Last();
                float face = dir < 0 ? o.XMax + run.Tuning.feetHalfWidth : o.XMin - run.Tuning.feetHalfWidth;
                Assert.AreEqual(face, run.Match.Player.Position.x, 0.01f, "the runner stops at the obstacle face");
                Assert.AreEqual(py, run.Match.Player.Position.y, 1e-4f, "runs are left/right only");
            }
        }

        // The lanes D-047 keeps open: behind the truck and pool, and in front of the truck. A run there reaches the arena end.
        [TestCase(9f, -11.0f, 2.5f, -10f, -1, TestName = "Truck_RunBehindIt_Passes")]
        [TestCase(9f, -13.4f, 2.5f, -13.6f, -1, TestName = "Truck_RunInFrontLane_Passes")]
        [TestCase(17f, -11.0f, 24.5f, -10f, 1, TestName = "Pool_RunBehindIt_Passes")]
        public void ArenaPrefab_RunOutsideEnlargedFootprint_Passes(float px, float py, float cx, float cy, int dir)
        {
            using (var run = new PrefabArenaRun(new Vector2(px, py), new Vector2(cx, cy)))
            {
                ArenaRect o = dir < 0 ? run.Match.Arena.Obstacles.OrderBy(r => r.XMin).First() : run.Match.Arena.Obstacles.OrderBy(r => r.XMin).Last();
                float pastFace = dir < 0 ? o.XMax - 0.5f : o.XMin + 0.5f;
                run.RunUntil(() => dir < 0 ? run.Match.Player.Position.x < pastFace : run.Match.Player.Position.x > pastFace);
                Assert.AreEqual(0, run.ObstacleHits, "no crash outside the footprint");
                Assert.AreEqual(CombatState.Running, run.Match.Player.State, "still running past the obstacle face");
            }
        }

        /// <summary>A CombatMatch on the real Arena.prefab geometry; the player presses Run once and runs toward the CPU (D-037 Focus).</summary>
        private sealed class PrefabArenaRun : System.IDisposable
        {
            private const float Dt = 0.01f;
            public readonly CombatTuning Tuning;
            public readonly CombatMatch Match;
            public int ObstacleHits;

            public PrefabArenaRun(Vector2 playerStart, Vector2 cpuStart)
            {
#if UNITY_EDITOR
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Arena.prefab");
                Assert.IsNotNull(prefab, "Assets/Prefabs/Arena/Arena.prefab not found");
                GameObject instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                ArenaGeometry arena = ArenaObstacleMarker.BuildGeometry(instance.GetComponentsInChildren<ArenaObstacleMarker>());
                Object.DestroyImmediate(instance);
                Tuning = ScriptableObject.CreateInstance<CombatTuning>();
                Match = new CombatMatch(Tuning, arena, playerStart, cpuStart);
                Match.Events.ObstacleHit += (id, c) => { if (id == WrestlerId.Player) ObstacleHits++; };
#else
                Assert.Ignore("Needs the Editor AssetDatabase.");
#endif
            }

            public void RunUntil(System.Func<bool> until, float maxSeconds = 5f)
            {
                var press = new WrestlerIntent(Vector2.zero, ButtonState.None, new ButtonState(true, true), ButtonState.None);
                Match.Tick(Dt, press, WrestlerIntent.None);
                Assert.AreEqual(CombatState.Running, Match.Player.State, "setup: one Run press starts a run");
                for (float t = 0f; t < maxSeconds; t += Dt)
                {
                    if (until()) return;
                    Match.Tick(Dt, WrestlerIntent.None, WrestlerIntent.None);
                }
                Assert.Fail($"condition not reached within {maxSeconds} s (player at {Match.Player.Position}, {Match.Player.State})");
            }

            public void Dispose()
            {
                if (Tuning != null) Object.DestroyImmediate(Tuning);
            }
        }

        /// <summary>Values typed in from the ARCHITECTURE 3.1 table (world units).</summary>
        public static void AssertMatchesTable(ArenaGeometry g)
        {
            AssertRect("walkable band", g.Bounds, 1.5f, -13.6667f, 25.1667f, -9.6667f); // D-027 narrowed band (ARCHITECTURE 3.1)
            Assert.AreEqual(2, g.Obstacles.Count);
            ArenaRect truck = g.Obstacles.OrderBy(o => o.XMin).First();
            ArenaRect pool = g.Obstacles.OrderBy(o => o.XMin).Last();
            // D-035 designer positions (truck (-2, -13), pool (21, -14)); D-047 enlarged depths (rows 790-680 and 840-680).
            AssertRect("truck footprint", truck, -2f, -13.1667f, 6.3667f, -11.3333f);
            AssertRect("pool footprint", pool, 21f, -14f, 30.3333f, -11.3333f);
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
