using NUnit.Framework;
using RuralRumble.Core;
using UnityEditor;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>D-049 footstep surface map (ARCHITECTURE 4.8): the plain C# lookup and the project map on Arena.prefab.
    /// Probe points are canvas (x, row) of the D-048 backdrop converted to world (x / 60, -row / 60).</summary>
    public class SurfaceMapTests
    {
        private const string ArenaPrefabPath = "Assets/Prefabs/Arena/Arena.prefab";
        private const string MaskPath = "Assets/Data/Arena/ArenaSurfaceMap.png";

        private static Vector2 Canvas(float x, float row) => new Vector2(x / 60f, -row / 60f);

        // --- Plain lookup -------------------------------------------------------------------------------------------

        [Test]
        public void Classify_ReadsTheColourCode()
        {
            Assert.AreEqual(SurfaceType.Dirt, SurfaceMap.Classify(new Color32(0, 0, 0, 255)));
            Assert.AreEqual(SurfaceType.Grass, SurfaceMap.Classify(new Color32(0, 255, 0, 255)));
            Assert.AreEqual(SurfaceType.Puddle, SurfaceMap.Classify(new Color32(0, 0, 255, 255)));
            Assert.AreEqual(SurfaceType.Dirt, SurfaceMap.Classify(new Color32(255, 255, 255, 255)), "white is not a code");
            Assert.AreEqual(SurfaceType.Dirt, SurfaceMap.Classify(new Color32(255, 0, 0, 255)), "red is not a code");
        }

        [Test]
        public void FromPixels_FlipsUnityBottomUpRowsSoRowZeroIsTheTopOfTheArt()
        {
            // 2x2 texture, GetPixels32 order (bottom row first): bottom = grass, dirt; top = puddle, dirt.
            var pixels = new[]
            {
                new Color32(0, 255, 0, 255), new Color32(0, 0, 0, 255),
                new Color32(0, 0, 255, 255), new Color32(0, 0, 0, 255),
            };
            var map = SurfaceMap.FromPixels(pixels, 2, 2, Vector2.zero, 1f);

            Assert.AreEqual(SurfaceType.Puddle, map.CellAt(0, 0), "top-left");
            Assert.AreEqual(SurfaceType.Grass, map.CellAt(0, 1), "bottom-left");
            Assert.AreEqual(SurfaceType.Puddle, map.SurfaceAt(new Vector2(0.5f, -0.5f)), "world top-left texel");
            Assert.AreEqual(SurfaceType.Grass, map.SurfaceAt(new Vector2(0.5f, -1.5f)), "world bottom-left texel");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(1.5f, -1.5f)));
        }

        [Test]
        public void SurfaceAt_UsesTheTexelUnderThePoint_AndTheMapOrigin()
        {
            // 3 x 1 strip at world top-left (10, 5), 0.5 units per texel: dirt, grass, puddle.
            var map = new SurfaceMap(new[] { SurfaceType.Dirt, SurfaceType.Grass, SurfaceType.Puddle }, 3, 1,
                new Vector2(10f, 5f), 0.5f);

            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(10.0f, 4.9f)));
            Assert.AreEqual(SurfaceType.Grass, map.SurfaceAt(new Vector2(10.5f, 4.9f)), "texel boundary belongs to the next texel");
            Assert.AreEqual(SurfaceType.Grass, map.SurfaceAt(new Vector2(10.99f, 4.51f)));
            Assert.AreEqual(SurfaceType.Puddle, map.SurfaceAt(new Vector2(11.4f, 4.6f)));
        }

        [Test]
        public void SurfaceAt_OffTheMap_IsDirt()
        {
            var map = new SurfaceMap(new[] { SurfaceType.Puddle }, 1, 1, Vector2.zero, 1f);

            Assert.AreEqual(SurfaceType.Puddle, map.SurfaceAt(new Vector2(0.5f, -0.5f)));
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(-0.01f, -0.5f)), "left");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(1.0f, -0.5f)), "right edge is outside");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(0.5f, 0.01f)), "above");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(0.5f, -1.0f)), "below");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(float.NaN, -0.5f)), "NaN");
        }

        // --- Project map (D-048 yard) -------------------------------------------------------------------------------

        private static ArenaSurfaceMap LoadArenaMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.IsNotNull(prefab, ArenaPrefabPath);
            var map = prefab.GetComponent<ArenaSurfaceMap>();
            Assert.IsNotNull(map, "Arena.prefab root has an ArenaSurfaceMap");
            map.Rebuild();
            return map;
        }

        [Test]
        public void ArenaPrefab_MaskIsTheReadableProjectMap()
        {
            var map = LoadArenaMap();
            Assert.IsNotNull(map.Mask, "mask assigned");
            Assert.AreEqual(MaskPath, AssetDatabase.GetAssetPath(map.Mask));
            Assert.IsTrue(map.Mask.isReadable, "Read/Write enabled");
            Assert.AreEqual(FilterMode.Point, map.Mask.filterMode);
            Assert.AreEqual(540, map.Mask.width, "1 texel per 4 native px of the 2160-px backdrop");
            Assert.AreEqual(270, map.Mask.height);
            Assert.IsNotNull(map.Map);
            Assert.AreEqual(4f / 60f, map.Map.WorldUnitsPerTexel, 1e-6f);
            Assert.AreEqual(Vector2.zero, map.Map.WorldTopLeft);
        }

        [TestCase(600f, 840f, TestName = "Puddle_LeftPuddleReflection")]
        [TestCase(860f, 835f, TestName = "Puddle_LeftPuddleRightLobe")]
        [TestCase(1120f, 880f, TestName = "Puddle_MiddlePuddle")]
        [TestCase(1620f, 880f, TestName = "Puddle_RightPuddleSunReflection")]
        [TestCase(1800f, 940f, TestName = "Puddle_RightPuddleTrailerReflection")]
        public void ProjectMap_KnownPuddlePoints(float x, float row)
        {
            Assert.AreEqual(SurfaceType.Puddle, LoadArenaMap().SurfaceAt(Canvas(x, row)));
        }

        [TestCase(150f, 850f, TestName = "Grass_LeftField")]
        [TestCase(60f, 1000f, TestName = "Grass_LeftFieldFrontCorner")]
        [TestCase(600f, 745f, TestName = "Grass_BackStripAboveLeftPuddle")]
        [TestCase(990f, 820f, TestName = "Grass_ClumpRightOfLeftPuddle")]
        [TestCase(1150f, 790f, TestName = "Grass_StripedStripLeftOfPorchPath")]
        [TestCase(1800f, 820f, TestName = "Grass_RightLawn")]
        [TestCase(2020f, 1000f, TestName = "Grass_RightEdge")]
        public void ProjectMap_KnownGrassPoints(float x, float row)
        {
            Assert.AreEqual(SurfaceType.Grass, LoadArenaMap().SurfaceAt(Canvas(x, row)));
        }

        [TestCase(820f, 900f, TestName = "Dirt_PlayerSpawn")]
        [TestCase(1340f, 900f, TestName = "Dirt_CpuSpawn")]
        [TestCase(1420f, 760f, TestName = "Dirt_PorchPath")]
        [TestCase(400f, 1000f, TestName = "Dirt_FrontLeft")]
        [TestCase(1080f, 1030f, TestName = "Dirt_FrontMiddle")]
        [TestCase(1930f, 960f, TestName = "Dirt_BetweenRightPuddleAndLawn")]
        [TestCase(1080f, 300f, TestName = "Dirt_TrailerAboveTheYardIsUnmarked")]
        public void ProjectMap_KnownDirtPoints(float x, float row)
        {
            Assert.AreEqual(SurfaceType.Dirt, LoadArenaMap().SurfaceAt(Canvas(x, row)));
        }

        [Test]
        public void ProjectMap_OutOfBounds_IsDirt()
        {
            var map = LoadArenaMap();
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(-3f, -15f)), "left of the art (truck overhang)");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(38f, -15f)), "right of the art (pool overhang)");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(18f, 2f)), "above the art");
            Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(2f, -18.5f)), "below the art (left field would be grass)");
        }

        [Test]
        public void ArenaSurfaceMap_WithNoMask_ReturnsDirt()
        {
            var go = new GameObject("SurfaceMapTest");
            try
            {
                var map = go.AddComponent<ArenaSurfaceMap>();
                Assert.IsNull(map.Map);
                Assert.AreEqual(SurfaceType.Dirt, map.SurfaceAt(new Vector2(10f, -14f)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
