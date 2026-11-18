using System.Collections;
using System.Linq;
using NUnit.Framework;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// D-046 theatrics wiring in Match.unity (ARCHITECTURE 4.7): exactly one CombatTheatrics instance at the scene root,
    /// still enabled after Start (it turns itself off when it finds no running match), and a runtime "JuiceFlash"
    /// SpriteRenderer under each wrestler for the red flash. The component is found by type name so this assembly does
    /// not need a reference to RuralRumble.Juice.
    /// </summary>
    public class TheatricsSceneTests
    {
        private readonly MatchSceneLoader loader = new MatchSceneLoader();

        [UnityTearDown]
        public IEnumerator UnloadMatch() => loader.Unload();

        [UnityTest]
        public IEnumerator CombatTheatrics_OnceAtSceneRoot_EnabledAfterStart_FlashUnderBothWrestlers()
        {
            yield return loader.Load(silenceCpu: true);
            yield return null; // CombatTheatrics.Start runs at execution order 100, after MatchBootstrap

            MonoBehaviour[] theatrics = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(m => m != null && m.GetType().Name == "CombatTheatrics").ToArray();
            Assert.AreEqual(1, theatrics.Length, "ARCHITECTURE 4.7: one CombatTheatrics instance in Match.unity");
            MonoBehaviour t = theatrics[0];
            Assert.IsNull(t.transform.parent, "at the root of Match.unity");
            Assert.IsTrue(t.gameObject.activeInHierarchy, "GameObject active");
            Assert.IsTrue(t.enabled, "still enabled after Start (it disables itself only when there is no running match)");

            MatchBootstrap boot = MatchBootstrap.Current;
            foreach (Transform root in new[] { boot.PlayerRoot, boot.CpuRoot })
            {
                SpriteRenderer[] flashes = root.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r => r.gameObject.name == "JuiceFlash").ToArray();
                Assert.AreEqual(1, flashes.Length, $"{root.name}: one runtime JuiceFlash SpriteRenderer (ARCHITECTURE 4.7 Red flash)");
            }
            Assert.AreEqual(1f, Time.timeScale, "theatrics never set Time.timeScale");
        }
    }
}
