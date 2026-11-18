using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// ARCHITECTURE 3.1 "Arena rectangles": ArenaObstacleMarkers on the real Arena.prefab produce the
    /// walkable band and the truck/pool footprints in the table (D-048 layout).
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

        // D-048 layout with D-047 depth rules: each footprint covers its sprite's base from the band's back edge (row 735)
        // down to the truck's front tire (row 833) / the pool's base (row 884). A run into the back or the front strip of
        // a footprint crashes: Struck + one ObstacleHit, stopped at the obstacle face.
        [TestCase(10f, -12.4f, 3f, -16f, -1, TestName = "Truck_RunIntoBackOfFootprint_Crashes")]
        [TestCase(10f, -13.8f, 3f, -16f, -1, TestName = "Truck_RunIntoFrontStrip_Crashes")]
        [TestCase(24f, -12.4f, 33f, -16f, 1, TestName = "Pool_RunIntoBackOfFootprint_Crashes")]
        [TestCase(24f, -14.65f, 33f, -16f, 1, TestName = "Pool_RunIntoFrontStrip_Crashes")]
        public void ArenaPrefab_RunIntoFootprint_Crashes(float px, float py, float cx, float cy, int dir)
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

        // The lane D-048 leaves open: in front of both obstacles (the footprints reach the band's back edge, so there is
        // no lane behind them). A run there passes the obstacle face without a crash.
        [TestCase(10f, -14.0f, 3f, -16.5f, -1, TestName = "Truck_RunInFrontLane_Passes")]
        [TestCase(24f, -14.9f, 33.5f, -16.5f, 1, TestName = "Pool_RunInFrontLane_Passes")]
        public void ArenaPrefab_RunOutsideFootprint_Passes(float px, float py, float cx, float cy, int dir)
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

        // D-048 walkable band corners (feet x 2.0..34.0 = canvas x 120..2040, rows 735..1040). The front corners are
        // reachable exactly; the back corners lie inside the truck / pool footprints, so walking toward them stops on the
        // band's back edge at the obstacle face.
        [TestCase(4f, -16f, -1f, -1f, 2.0f, -17.3333f, TestName = "Band_FrontLeftCorner")]
        [TestCase(32f, -16f, 1f, -1f, 34.0f, -17.3333f, TestName = "Band_FrontRightCorner")]
        [TestCase(10f, -13f, -1f, 1f, 6.1f, -12.25f, TestName = "Band_BackLeft_StopsAtTruck")]
        [TestCase(26f, -13f, 1f, 1f, 29.4f, -12.25f, TestName = "Band_BackRight_StopsAtPool")]
        public void ArenaPrefab_WalkToBandCorner(float px, float py, float sx, float sy, float ex, float ey)
        {
            using (var run = new PrefabArenaRun(new Vector2(px, py), new Vector2(18f, -15f)))
            {
                var walk = new WrestlerIntent(new Vector2(sx, sy).normalized, ButtonState.None, ButtonState.None, ButtonState.None);
                for (float t = 0f; t < 8f; t += 0.01f)
                    run.Match.Tick(0.01f, walk, WrestlerIntent.None);
                Assert.AreEqual(ex, run.Match.Player.Position.x, 0.01f, "corner x");
                Assert.AreEqual(ey, run.Match.Player.Position.y, 0.01f, "corner y");
                Assert.AreEqual(0, run.ObstacleHits, "walking never crashes");
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

        /// <summary>Values typed in from the ARCHITECTURE 3.1 table (world units, D-048 layout).</summary>
        public static void AssertMatchesTable(ArenaGeometry g)
        {
            // Feet rows 735 (bottom of the porch steps) to 1040, canvas x 90..2070 (feet x 120..2040 with feetHalfWidth 0.5).
            AssertRect("walkable band", g.Bounds, 1.5f, -17.3333f, 34.5f, -12.25f);
            Assert.AreEqual(2, g.Obstacles.Count);
            ArenaRect truck = g.Obstacles.OrderBy(o => o.XMin).First();
            ArenaRect pool = g.Obstacles.OrderBy(o => o.XMin).Last();
            // Truck at (-2.7667, -13.7167) (canvas x -166, base row 823): rows 735-833. Pool at (29.9, -14.7333)
            // (canvas x 1794, base row 884): rows 735-884.
            AssertRect("truck footprint", truck, -2.7667f, -13.8834f, 5.6f, -12.25f);
            AssertRect("pool footprint", pool, 29.9f, -14.7333f, 39.2333f, -12.25f);
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
