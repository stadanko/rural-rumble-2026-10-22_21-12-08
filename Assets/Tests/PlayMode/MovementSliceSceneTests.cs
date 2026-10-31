using System.Collections;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using RuralRumble.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// Match.unity wiring (ARCHITECTURE 3, 3.1, 4.4 bootstrap order) and the player input path
    /// (ARCHITECTURE 4.1 PlayerInputSource -> CombatMatch). Each test loads Match fresh and unloads it after.
    /// </summary>
    public class MovementSliceSceneTests
    {
        // ARCHITECTURE 4.4 "Bootstrap order (built)": player (9.0, -11.6667), CPU (17.6667, -11.6667).
        private static readonly Vector2 PlayerSpawn = new Vector2(9.0f, -11.6667f);
        private static readonly Vector2 CpuSpawn = new Vector2(17.6667f, -11.6667f);

        private Scene matchScene;

        [UnitySetUp]
        public IEnumerator LoadMatch()
        {
            GameSession.Reset(); // Match opened directly: roster entry 0 vs 1 (ARCHITECTURE 4.4)
            SceneManager.LoadScene("Match", LoadSceneMode.Single);
            yield return null; // Awake/Start
            yield return null; // first ticks
            matchScene = SceneManager.GetSceneByName("Match");
            Assert.IsTrue(matchScene.isLoaded, "Match.unity did not load");
        }

        [UnityTearDown]
        public IEnumerator UnloadMatch()
        {
            Scene empty = SceneManager.CreateScene("QA_Empty_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            if (matchScene.IsValid() && matchScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(matchScene);
            GameSession.Reset();
        }

        [UnityTest]
        public IEnumerator Match_BuildsTwoWrestlersAtSpawnPoints_AndBindsViewsAndCamera()
        {
            MatchBootstrap boot = MatchBootstrap.Current;
            Assert.IsNotNull(boot, "MatchBootstrap.Current is null");
            CombatMatch match = boot.Match;
            Assert.IsNotNull(match, "MatchBootstrap.Match is null after Start");

            V2Assert(PlayerSpawn, match.Player.Position, 1e-3f, "player spawn");
            V2Assert(CpuSpawn, match.Cpu.Position, 1e-3f, "CPU spawn");
            Assert.AreEqual(WrestlerId.Player, match.Player.Id);
            Assert.AreEqual(WrestlerId.Cpu, match.Cpu.Id);

            // CombatMatchRunner writes each root's position from IWrestlerView.Position (4.2).
            V2Assert(match.Player.Position, boot.PlayerRoot.position, 1e-4f, "player root");
            V2Assert(match.Cpu.Position, boot.CpuRoot.position, 1e-4f, "CPU root");

            // Views bound to the right wrestler.
            var pv = boot.PlayerRoot.GetComponent<WrestlerView>();
            var cv = boot.CpuRoot.GetComponent<WrestlerView>();
            Assert.IsNotNull(pv); Assert.IsNotNull(cv);
            Assert.AreSame(match.Player, pv.View);
            Assert.AreSame(match.Cpu, cv.View);

            // Facing each other at start: Barry art faces left, so the player (facing right) is flipped.
            Assert.IsTrue(pv.SpriteRenderer.flipX, "player faces right -> flipped");
            Assert.IsFalse(cv.SpriteRenderer.flipX, "CPU faces left -> not flipped");

            // Arena geometry built from the scene's markers matches the ARCHITECTURE 3.1 table.
            ArenaPrefabTests.AssertMatchesTable(match.Arena);

            // Camera follows the wrestlers: force a 16:9 target and check the default framing (3.1).
            var cam = Object.FindFirstObjectByType<MatchCamera>();
            Assert.IsNotNull(cam, "no MatchCamera in Match.unity");
            Assert.IsNotNull(cam.TargetCamera);
            Assert.IsTrue(cam.TargetCamera.orthographic);
            cam.TargetCamera.aspect = 16f / 9f;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(4.5f, cam.TargetCamera.orthographicSize, 1e-3f, "default zoom at 16:9");
            float mid = (PlayerSpawn.x + CpuSpawn.x) * 0.5f;
            Assert.AreEqual(mid, cam.transform.position.x, 0.02f, "camera centers on the wrestlers' midpoint");
            Assert.AreEqual(-570f / 60f, cam.transform.position.y, 0.02f, "default view centered on canvas row 570");
        }

        [UnityTest]
        public IEnumerator Match_CpuStandInStaysIdle()
        {
            CombatMatch match = MatchBootstrap.Current.Match;
            yield return new WaitForSeconds(0.5f);
            V2Assert(CpuSpawn, match.Cpu.Position, 1e-3f, "idle CPU moved");
            Assert.AreEqual(CombatState.Standing, match.Cpu.State);
        }

        // ARCHITECTURE 4.1 / D-019: keyboard D = move right, A = left, Space = Run (press once, D-020).
        [UnityTest]
        public IEnumerator PlayerInput_Keyboard_WalksAndRuns()
        {
            MatchBootstrap boot = MatchBootstrap.Current;
            CombatMatch match = boot.Match;
            Assert.IsNotNull(boot.GetComponent<PlayerInputSource>(), "MatchBootstrap did not add PlayerInputSource");

            using (var keyboard = new TestKeyboard())
            {
                Vector2 start = match.Player.Position;

                keyboard.SetPressed("A");
                yield return new WaitForSeconds(0.4f);
                Assert.AreEqual(CombatState.Walking, match.Player.State, "holding A should walk");
                Assert.Less(match.Player.Position.x, start.x - 0.3f, "holding A should move the player left");
                Assert.AreEqual(-1, match.Player.HorizontalFacing);
                Assert.IsFalse(boot.PlayerRoot.GetComponent<WrestlerView>().SpriteRenderer.flipX, "facing left -> not flipped");

                keyboard.SetPressed("W");
                yield return new WaitForSeconds(0.3f);
                Assert.Greater(match.Player.Position.y, start.y + 0.2f, "holding W should move toward the trailer (+y)");

                keyboard.ReleaseAll();
                yield return null;
                yield return null;
                Assert.AreEqual(CombatState.Standing, match.Player.State, "releasing all keys should stand");

                // Press-once run: tap Space, release, and the run keeps going left (last horizontal facing).
                Vector2 beforeRun = match.Player.Position;
                keyboard.SetPressed("Space");
                yield return null;
                yield return null;
                keyboard.ReleaseAll();
                yield return new WaitForSeconds(0.2f);
                Assert.AreEqual(CombatState.Running, match.Player.State, "one Space tap should start a run");
                Assert.Less(match.Player.Position.x, beforeRun.x - 0.5f);
                Assert.AreEqual(beforeRun.y, match.Player.Position.y, 1e-4f, "runs are left/right only");

                V2Assert(CpuSpawn, match.Cpu.Position, 1e-3f, "player input must not move the CPU");
            }
        }

        private static void V2Assert(Vector2 expected, Vector3 actual, float tol, string msg)
        {
            Assert.That(Vector2.Distance(expected, actual), Is.LessThanOrEqualTo(tol), $"{msg}: expected {expected:F4}, was {(Vector2)actual:F4}");
        }
    }
}
