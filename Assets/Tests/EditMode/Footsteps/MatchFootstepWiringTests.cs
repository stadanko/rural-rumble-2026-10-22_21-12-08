using System.Linq;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.Juice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-049 wiring in the saved Match.unity (ARCHITECTURE 4.8, ACCEPTANCE FS1.6): exactly one enabled FootstepAudio at
    /// the scene root with its settings, and MatchBootstrap's serialized surfaceMap pointing at the Arena's
    /// ArenaSurfaceMap (set explicitly, not left to the FindAnyObjectByType fallback). Reads the scene file through a
    /// preview scene, so no Awake runs and the scene open in the Editor is untouched.
    /// </summary>
    public class MatchFootstepWiringTests
    {
        private const string MatchScenePath = "Assets/Scenes/Match.unity";

        [Test]
        public void MatchScene_HasOneEnabledFootstepAudioAtRoot_AndBootstrapSurfaceMapIsSet()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(MatchScenePath);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();

                FootstepAudio[] players = roots.SelectMany(r => r.GetComponentsInChildren<FootstepAudio>(true)).ToArray();
                Assert.AreEqual(1, players.Length, "ARCHITECTURE 4.8: exactly one FootstepAudio in Match.unity");
                FootstepAudio player = players[0];
                Assert.IsNull(player.transform.parent, "FootstepAudio sits at the scene root");
                Assert.AreEqual("FootstepAudio", player.gameObject.name);
                Assert.IsTrue(player.gameObject.activeSelf, "FootstepAudio GameObject active");
                Assert.IsTrue(player.enabled, "FootstepAudio component enabled");
                Assert.IsNotNull(player.Settings, "FootstepAudio settings assigned (from the prefab)");
                Assert.AreEqual("Assets/Prefabs/Effects/FootstepAudio.prefab",
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject), "an instance of the FootstepAudio prefab");
                Assert.IsNull(player.GetComponent("CombatAudio"), "separate from CombatAudio");

                GameObject arena = roots.Single(r => r.name == "Arena");
                ArenaSurfaceMap arenaMap = arena.GetComponent<ArenaSurfaceMap>();
                Assert.IsNotNull(arenaMap, "the Arena instance carries ArenaSurfaceMap");

                // MatchBootstrap lives in RuralRumble.Match, which this assembly does not reference: read it by type name.
                MonoBehaviour bootstrap = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Single(m => m != null && m.GetType().Name == "MatchBootstrap");
                SerializedProperty surfaceMap = new SerializedObject(bootstrap).FindProperty("surfaceMap");
                Assert.IsNotNull(surfaceMap, "MatchBootstrap has a serialized surfaceMap field");
                Assert.AreSame(arenaMap, surfaceMap.objectReferenceValue,
                    "MatchBootstrap.surfaceMap is set explicitly to the Arena's ArenaSurfaceMap");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
