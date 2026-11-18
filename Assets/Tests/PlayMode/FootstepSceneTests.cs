using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Juice;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// D-049 footsteps in the running Match.unity (ARCHITECTURE 4.8, ACCEPTANCE FS1.6): one enabled FootstepAudio,
    /// the bootstrap's SurfaceMap is the Arena's, and a walking player raises foot contacts that FootstepAudio plays
    /// (the played part is skipped when the git-ignored store audio is absent).
    /// </summary>
    public class FootstepSceneTests
    {
        private readonly MatchSceneLoader loader = new MatchSceneLoader();

        [UnityTearDown]
        public IEnumerator UnloadMatch() => loader.Unload();

        [UnityTest]
        public IEnumerator Match_WalkingPlayer_RaisesContacts_AndPlaysWalkSteps()
        {
            yield return loader.Load(silenceCpu: true);
            MatchBootstrap boot = MatchBootstrap.Current;

            FootstepAudio[] players = Object.FindObjectsByType<FootstepAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, players.Length, "one FootstepAudio in Match.unity");
            FootstepAudio audio = players[0];
            Assert.IsTrue(audio.isActiveAndEnabled, "FootstepAudio enabled");
            Assert.IsNotNull(boot.SurfaceMap, "MatchBootstrap.SurfaceMap is set");
            Assert.AreSame(boot.SurfaceMap, boot.SurfaceMap.gameObject.GetComponent<ArenaSurfaceMap>());
            Assert.AreEqual("Arena", boot.SurfaceMap.gameObject.name, "the Arena instance's ArenaSurfaceMap");

            var contacts = new List<FootContact>();
            var played = new List<FootstepPlayback>();
            void OnContact(FootContact c) { if (c.Wrestler == WrestlerId.Player) contacts.Add(c); }
            void OnPlayed(FootstepPlayback p) { if (p.Contact.Wrestler == WrestlerId.Player) played.Add(p); }
            FootContactEvents.Contact += OnContact;
            audio.Played += OnPlayed;
            try
            {
                using (var keyboard = new TestKeyboard())
                {
                    // Walk left from the player spawn (13.67, -15): open dirt/grass ground in front of the truck.
                    keyboard.SetPressed("A");
                    yield return new WaitForSeconds(1.5f);
                    Assert.AreEqual(CombatState.Walking, boot.Match.Player.State, "holding A walks");
                    keyboard.ReleaseAll();
                    yield return null;
                }
            }
            finally
            {
                FootContactEvents.Contact -= OnContact;
                audio.Played -= OnPlayed;
            }

            Assert.GreaterOrEqual(contacts.Count, 1, "1.5 s of walking raises at least one foot contact for the player");
            Assert.IsTrue(contacts.All(c => c.Gait == Gait.Walk), "walking contacts are Walk");

            bool anyClip = audio.Settings != null && new[] { SurfaceType.Dirt, SurfaceType.Grass, SurfaceType.Puddle }
                .Select(s => audio.Settings.SetFor(s, Gait.Walk))
                .Any(set => set != null && set.clips != null && Enumerable.Range(0, set.clips.Length).Any(set.IsPlayable));
            if (!anyClip)
                Assert.Ignore("store footstep audio is not present (git-ignored, D-034 amendment); contacts checked, playback skipped");

            Assert.GreaterOrEqual(played.Count, 1, "FootstepAudio plays at least one step for the walking player");
            foreach (FootstepPlayback p in played)
            {
                Assert.AreEqual(Gait.Walk, p.Contact.Gait);
                Assert.AreEqual(FootstepRules.SetFor(p.Surface, Gait.Walk), p.Set, "walk set for the surface under the foot");
                Assert.AreEqual(boot.SurfaceMap.SurfaceAt(p.Contact.Position), p.Surface, "surface read from the bootstrap's map");
                Assert.IsNotNull(p.Clip);
            }
        }
    }
}
