using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEditor;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>D-049 footstep decisions (ARCHITECTURE 4.8, Juice side).</summary>
    public class FootstepRulesTests
    {
        private const string SettingsPath = "Assets/Prefabs/Effects/FootstepSettings.asset";
        private const string PrefabPath = "Assets/Prefabs/Effects/FootstepAudio.prefab";

        // ---- Set selection ----

        [TestCase(SurfaceType.Dirt, Gait.Walk, FootstepSetId.DirtWalk)]
        [TestCase(SurfaceType.Dirt, Gait.Run, FootstepSetId.DirtRun)]
        [TestCase(SurfaceType.Grass, Gait.Walk, FootstepSetId.GrassWalk)]
        [TestCase(SurfaceType.Grass, Gait.Run, FootstepSetId.GrassRun)]
        [TestCase(SurfaceType.Puddle, Gait.Walk, FootstepSetId.PuddleWalk)]
        [TestCase(SurfaceType.Puddle, Gait.Run, FootstepSetId.PuddleRun)]
        public void SetFor_SurfaceAndGait(SurfaceType surface, Gait gait, FootstepSetId expected)
        {
            Assert.AreEqual(expected, FootstepRules.SetFor(surface, gait));
        }

        [Test]
        public void SetFor_UnknownSurface_IsDirt()
        {
            Assert.AreEqual(FootstepSetId.DirtWalk, FootstepRules.SetFor((SurfaceType)99, Gait.Walk));
            Assert.AreEqual(FootstepSetId.DirtRun, FootstepRules.SetFor((SurfaceType)99, Gait.Run));
        }

        [Test]
        public void Settings_SetFor_ReturnsEachField()
        {
            var s = ScriptableObject.CreateInstance<FootstepSettings>();
            try
            {
                Assert.AreSame(s.dirtWalk, s.SetFor(SurfaceType.Dirt, Gait.Walk));
                Assert.AreSame(s.dirtRun, s.SetFor(SurfaceType.Dirt, Gait.Run));
                Assert.AreSame(s.grassWalk, s.SetFor(SurfaceType.Grass, Gait.Walk));
                Assert.AreSame(s.grassRun, s.SetFor(SurfaceType.Grass, Gait.Run));
                Assert.AreSame(s.puddleWalk, s.SetFor(SurfaceType.Puddle, Gait.Walk));
                Assert.AreSame(s.puddleRun, s.SetFor(SurfaceType.Puddle, Gait.Run));
                Assert.IsNull(s.SetFor((FootstepSetId)42));
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        // ---- No immediate repeat ----

        [Test]
        public void Picker_NeverRepeatsAndCoversAllVariants()
        {
            var picker = new VariantPicker();
            var rng = new System.Random(1234);
            var seen = new HashSet<int>();
            int last = -1;
            for (int i = 0; i < 500; i++)
            {
                int v = picker.Pick(5, null, (float)rng.NextDouble());
                Assert.That(v, Is.InRange(0, 4));
                Assert.AreNotEqual(last, v, $"repeat at step {i}");
                seen.Add(v);
                last = v;
            }
            Assert.AreEqual(5, seen.Count);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(0.999f)]
        [TestCase(1f)]
        public void Picker_TwoVariants_StrictlyAlternate(float roll)
        {
            var picker = new VariantPicker();
            int first = picker.Pick(2, null, roll);
            for (int i = 1; i < 10; i++)
                Assert.AreEqual((first + i) % 2, picker.Pick(2, null, roll));
        }

        [Test]
        public void Picker_OneVariant_Repeats()
        {
            var picker = new VariantPicker();
            Assert.AreEqual(0, picker.Pick(1, null, 0.7f));
            Assert.AreEqual(0, picker.Pick(1, null, 0.2f));
        }

        [Test]
        public void Picker_RollEdges_StayInRange()
        {
            var picker = new VariantPicker();
            Assert.AreEqual(0, picker.Pick(5, null, 0f));
            Assert.AreEqual(4, picker.Pick(5, null, 1f));
            Assert.AreEqual(3, picker.Pick(5, null, 1f)); // 4 skipped
            Assert.AreEqual(0, picker.Pick(5, null, -3f));
        }

        // ---- Missing clips ----

        [Test]
        public void Picker_SkipsMissingClips()
        {
            var picker = new VariantPicker();
            bool[] present = { false, true, false, true, false };
            int last = -1;
            for (int i = 0; i < 20; i++)
            {
                int v = picker.Pick(5, k => present[k], (float)i / 20f);
                Assert.IsTrue(v == 1 || v == 3, $"picked missing variant {v}");
                Assert.AreNotEqual(last, v, "two present variants must alternate");
                last = v;
            }
        }

        [Test]
        public void Picker_OnlyOnePresent_PlaysIt_AndNoneMeansMinusOne()
        {
            var picker = new VariantPicker();
            Assert.AreEqual(2, picker.Pick(3, k => k == 2, 0.1f));
            Assert.AreEqual(2, picker.Pick(3, k => k == 2, 0.9f));
            Assert.AreEqual(-1, picker.Pick(3, k => false, 0.5f));
            Assert.AreEqual(-1, picker.Pick(0, null, 0.5f));
        }

        [Test]
        public void Picker_LastVariantGoneMissing_StillPicks()
        {
            var picker = new VariantPicker();
            Assert.AreEqual(1, picker.Pick(2, null, 0.9f));
            Assert.AreEqual(0, picker.Pick(2, k => k == 0, 0.9f));
        }

        [Test]
        public void Set_IsPlayable_FalseForNullOrEmptyEntries()
        {
            var set = new FootstepSet { clips = new[] { null, new FootstepClip(null, -3f) } };
            Assert.IsFalse(set.IsPlayable(0));
            Assert.IsFalse(set.IsPlayable(1));
            Assert.IsFalse(set.IsPlayable(-1));
            Assert.IsFalse(set.IsPlayable(2));
            Assert.IsFalse(new FootstepSet { clips = null }.IsPlayable(0));
        }

        // ---- Gain maths ----

        [Test]
        public void DbToLinear_KnownValues()
        {
            Assert.AreEqual(1f, FootstepRules.DbToLinear(0f), 1e-6f);
            Assert.AreEqual(0.5012f, FootstepRules.DbToLinear(-6f), 1e-4f);
            Assert.AreEqual(1.4125f, FootstepRules.DbToLinear(3f), 1e-4f);
            Assert.AreEqual(-6f, FootstepRules.LinearToDb(FootstepRules.DbToLinear(-6f)), 1e-4f);
        }

        [TestCase(0f, -1f)]
        [TestCase(0.5f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(-2f, -1f)]
        [TestCase(5f, 1f)]
        public void JitterDb_SpreadsEvenly(float roll, float expected)
        {
            Assert.AreEqual(expected, FootstepRules.JitterDb(1f, roll), 1e-6f);
        }

        [Test]
        public void JitterDb_NegativeVariation_IsZero()
        {
            Assert.AreEqual(0f, FootstepRules.JitterDb(-2f, 1f), 1e-6f);
        }

        [Test]
        public void Volume_IsMasterTimesSummedDb()
        {
            // 0.6 x 10^((-2 + -4.8 + 1) / 20)
            float expected = 0.6f * Mathf.Pow(10f, -5.8f / 20f);
            Assert.AreEqual(expected, FootstepRules.Volume(0.6f, -2f, -4.8f, 1f), 1e-6f);
        }

        [Test]
        public void Volume_PositiveGainUnderHeadroom_IsNotClamped()
        {
            // Loudest registry gain (+3.3 dB, grass run 01) plus +1 dB variation under the 0.6 master.
            float raw = FootstepRules.RawVolume(0.6f, 0f, 3.3f, 1f);
            Assert.Less(raw, 1f);
            Assert.AreEqual(raw, FootstepRules.Volume(0.6f, 0f, 3.3f, 1f), 1e-6f);
        }

        [Test]
        public void Volume_ClampsToZeroOne()
        {
            Assert.AreEqual(1f, FootstepRules.Volume(1f, 0f, 3.3f, 1f), 1e-6f);
            Assert.Greater(FootstepRules.RawVolume(1f, 0f, 3.3f, 1f), 1f);
            Assert.AreEqual(0f, FootstepRules.Volume(-0.5f, 0f, 0f, 0f), 1e-6f);
            Assert.AreEqual(0f, FootstepRules.Volume(0f, 0f, 6f, 1f), 1e-6f);
        }

        [Test]
        public void MaxRawVolume_FlagsClipping()
        {
            var s = ScriptableObject.CreateInstance<FootstepSettings>();
            try
            {
                s.masterVolume = 1f;
                s.levelVariationDb = 1f;
                s.grassRun.clips = new[] { new FootstepClip(null, 3.3f) };
                Assert.Greater(s.MaxRawVolume(), 1f);
                s.masterVolume = 0.6f;
                Assert.LessOrEqual(s.MaxRawVolume(), 1f);
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        // ---- Project asset and prefab ----

        private static readonly Dictionary<FootstepSetId, float[]> RegistryGains = new Dictionary<FootstepSetId, float[]>
        {
            { FootstepSetId.DirtWalk, new[] { -4.8f, -4.2f, -4.5f, -3.6f, -3.6f } },
            { FootstepSetId.DirtRun, new[] { -1.8f, -0.5f, -2.5f, 2.9f, -0.8f } },
            { FootstepSetId.GrassWalk, new[] { -1.9f, -1.3f } },
            { FootstepSetId.GrassRun, new[] { 3.3f, -0.3f } },
            { FootstepSetId.PuddleWalk, new[] { -0.9f, 0.0f } },
            { FootstepSetId.PuddleRun, new[] { 1.8f, 2.2f } },
        };

        [Test]
        public void ProjectSettings_MatchRegistryGains_AndNeverClamp()
        {
            var s = AssetDatabase.LoadAssetAtPath<FootstepSettings>(SettingsPath);
            Assert.IsNotNull(s, SettingsPath);
            foreach (KeyValuePair<FootstepSetId, float[]> kv in RegistryGains)
            {
                FootstepSet set = s.SetFor(kv.Key);
                Assert.AreEqual(kv.Value.Length, set.clips.Length, kv.Key.ToString());
                Assert.AreEqual(0f, set.trimDb, 1e-6f, kv.Key + " trim");
                for (int i = 0; i < kv.Value.Length; i++)
                    Assert.AreEqual(kv.Value[i], set.clips[i].gainDb, 1e-4f, $"{kv.Key}[{i}]");
            }
            Assert.LessOrEqual(s.MaxRawVolume(), 1f, "default master leaves no headroom for the loudest variant");
            Assert.AreEqual(1f, s.levelVariationDb, 1e-6f);
            Assert.AreEqual(0f, s.pitchVariation, 1e-6f);
        }

        [Test]
        public void ProjectSettings_ClipsThatArePresent_AreInTheRightFolder()
        {
            // Store audio is git-ignored: an absent clip is fine, a present one must be its set's own.
            var s = AssetDatabase.LoadAssetAtPath<FootstepSettings>(SettingsPath);
            Assert.IsNotNull(s, SettingsPath);
            string[] folders = { "dirt_walk", "dirt_run", "grass_walk", "grass_run", "puddle_walk", "puddle_run" };
            for (int id = 0; id < FootstepRules.SetCount; id++)
            {
                FootstepSet set = s.SetFor((FootstepSetId)id);
                for (int i = 0; i < set.clips.Length; i++)
                {
                    AudioClip clip = set.clips[i].clip;
                    if (clip == null)
                        continue;
                    string path = AssetDatabase.GetAssetPath(clip);
                    StringAssert.Contains("/Footsteps/" + folders[id] + "/", path, $"{(FootstepSetId)id}[{i}]");
                    StringAssert.EndsWith($"_{i + 1:00}.wav", path, $"{(FootstepSetId)id}[{i}] order");
                }
            }
        }

        [Test]
        public void Prefab_HasFootstepAudio_WithSettingsAssigned()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var audio = prefab.GetComponent<FootstepAudio>();
            Assert.IsNotNull(audio);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<FootstepSettings>(SettingsPath), audio.Settings);
        }
    }
}
