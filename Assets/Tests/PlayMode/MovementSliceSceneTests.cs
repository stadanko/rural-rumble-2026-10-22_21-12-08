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
    ///
    /// Since combat-core step 4 the CPU is driven by OpponentBrain, so it walks toward the player from the first
    /// tick. Tests about spawning and player movement load the scene with the CPU silenced (see
    /// <see cref="MatchSceneLoader"/>); the brain wiring is checked with the real brain.
    /// </summary>
    public class MovementSliceSceneTests
    {
        // ARCHITECTURE 4.4 "Bootstrap order (built)": player (9.0, -11.6667), CPU (17.6667, -11.6667).
        internal static readonly Vector2 PlayerSpawn = new Vector2(9.0f, -11.6667f);
        internal static readonly Vector2 CpuSpawn = new Vector2(17.6667f, -11.6667f);

        private readonly MatchSceneLoader loader = new MatchSceneLoader();

        [UnityTearDown]
        public IEnumerator UnloadMatch() => loader.Unload();

        [UnityTest]
        public IEnumerator Match_BuildsTwoWrestlersAtSpawnPoints_AndBindsViewsAndCamera()
        {
            yield return loader.Load(silenceCpu: true);
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

            // Arena geometry built from the scene's markers matches the ARCHITECTURE 3.1 table (D-027 band).
            ArenaPrefabTests.AssertMatchesTable(match.Arena);

            // Camera follows the wrestlers: force a 16:9 target and check the default framing (3.1).
            var cam = Object.FindFirstObjectByType<MatchCamera>();
            Assert.IsNotNull(cam, "no MatchCamera in Match.unity");
            Assert.IsNotNull(cam.TargetCamera);
            Assert.IsTrue(cam.TargetCamera.orthographic);
            cam.TargetCamera.aspect = 16f / 9f;
            yield return new WaitForSecondsRealtime(1.5f);
            V2Assert(CpuSpawn, match.Cpu.Position, 1e-3f, "silenced CPU stays put while the camera settles");
            Assert.AreEqual(4.5f, cam.TargetCamera.orthographicSize, 1e-3f, "default zoom at 16:9");
            float mid = (PlayerSpawn.x + CpuSpawn.x) * 0.5f;
            Assert.AreEqual(mid, cam.transform.position.x, 0.02f, "camera centers on the wrestlers' midpoint");
            Assert.AreEqual(-570f / 60f, cam.transform.position.y, 0.02f, "default view centered on canvas row 570");
        }

        // ARCHITECTURE 4.4 "CPU brain wiring (built)": MatchBootstrap.Start creates an OpponentBrain bound to the
        // match's CPU and player views and the CPU reads it. Replaces the movement slice's "CPU stays idle" check.
        [UnityTest]
        public IEnumerator Match_CpuIsDrivenByOpponentBrain_AndApproachesThePlayer()
        {
            yield return loader.Load(silenceCpu: false);
            MatchBootstrap boot = MatchBootstrap.Current;
            CombatMatch match = boot.Match;
            Assert.IsNotNull(boot.CpuBrain, "MatchBootstrap.CpuBrain is null after Start");
            Assert.IsTrue(boot.CpuBrain.IsBound, "the brain must be bound to the match views");

            float startGap = Mathf.Abs(match.Cpu.Position.x - match.Player.Position.x);
            yield return new WaitForSeconds(1.5f);
            float gap = Mathf.Abs(match.Cpu.Position.x - match.Player.Position.x);
            Assert.Less(gap, startGap - 1f, "the CPU should walk toward the idle player (COMBAT_SPEC CPU AI: Approach)");
            V2Assert(PlayerSpawn, match.Player.Position, 0.05f, "the player has no input and must not move");
        }

        // ARCHITECTURE 4.1 / D-019: keyboard D = move right, A = left, Space = Run (press once, D-020).
        [UnityTest]
        public IEnumerator PlayerInput_Keyboard_WalksAndRuns()
        {
            yield return loader.Load(silenceCpu: true);
            MatchBootstrap boot = MatchBootstrap.Current;
            CombatMatch match = boot.Match;
            Assert.IsNotNull(boot.GetComponent<PlayerInputSource>(), "MatchBootstrap did not add PlayerInputSource");

            using (var keyboard = new TestKeyboard())
            {
                Vector2 start = match.Player.Position;

                // D-035 put the truck at (-2, -13): its footprint (x -2..6.37, y -13..-11.83) fills the depth band
                // at and below the spawn's height to the left. Test the vertical keys from the spawn (nothing above or
                // below it), and end up behind the truck (above y -11.83) so the left walk and run below pass behind it.
                keyboard.SetPressed("W");
                yield return new WaitForSeconds(0.3f);
                Assert.Greater(match.Player.Position.y, start.y + 0.2f, "holding W should move toward the trailer (+y)");

                Vector2 afterUp = match.Player.Position;
                keyboard.SetPressed("S");
                yield return new WaitForSeconds(0.2f);
                Assert.Less(match.Player.Position.y, afterUp.y - 0.2f, "holding S should move away from the trailer (-y)");
                Assert.Greater(match.Player.Position.y, -11.8333f, "stay behind the truck for the left walk and run");

                Vector2 beforeWalk = match.Player.Position;
                keyboard.SetPressed("A");
                yield return new WaitForSeconds(0.4f);
                Assert.AreEqual(CombatState.Walking, match.Player.State, "holding A should walk");
                Assert.Less(match.Player.Position.x, beforeWalk.x - 0.3f, "holding A should move the player left");
                Assert.AreEqual(-1, match.Player.HorizontalFacing);
                Assert.IsFalse(boot.PlayerRoot.GetComponent<WrestlerView>().SpriteRenderer.flipX, "facing left -> not flipped");

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

                // The CPU is silenced in this test, so any CPU movement would have to come from the player's input.
                V2Assert(CpuSpawn, match.Cpu.Position, 1e-3f, "player input must not move the CPU");
            }
        }

        internal static void V2Assert(Vector2 expected, Vector3 actual, float tol, string msg)
        {
            Assert.That(Vector2.Distance(expected, actual), Is.LessThanOrEqualTo(tol), $"{msg}: expected {expected:F4}, was {(Vector2)actual:F4}");
        }
    }

    /// <summary>
    /// Loads Match.unity for a test and unloads it afterwards. With <c>silenceCpu</c>, the CPU reads
    /// <see cref="IdleInput"/> instead of the brain: in <see cref="SceneManager.sceneLoaded"/> (after every
    /// Awake, before any Start) it calls the public <see cref="CombatMatchRunner.Configure"/> with only a CPU
    /// input, which Configure documents as "call before Start; null keeps the existing value". No production
    /// code changes; MatchBootstrap still creates and exposes its brain, the runner just doesn't read it.
    /// </summary>
    internal sealed class MatchSceneLoader
    {
        private Scene matchScene;
        private bool silence;

        public IEnumerator Load(bool silenceCpu)
        {
            silence = silenceCpu;
            GameSession.Reset(); // Match opened directly: roster entry 0 vs 1 (ARCHITECTURE 4.4)
            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                SceneManager.LoadScene("Match", LoadSceneMode.Single);
                yield return null; // Awake/Start
                yield return null; // first ticks
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
            matchScene = SceneManager.GetSceneByName("Match");
            Assert.IsTrue(matchScene.isLoaded, "Match.unity did not load");
            Assert.IsNotNull(MatchBootstrap.Current, "MatchBootstrap.Current is null");
            Assert.IsNotNull(MatchBootstrap.Current.Match, "MatchBootstrap.Match is null after Start");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Match")
                return;
            // Tests decide the CPU's input themselves, whatever the designer's "Disable Cpu Ai" testing toggle is
            // set to in Match.unity: a test that wants the brain gets it even if the toggle is saved on.
            var bootstrap = Object.FindFirstObjectByType<MatchBootstrap>();
            if (bootstrap != null)
                bootstrap.CpuAiDisabled = false;
            if (!silence)
                return;
            var runner = Object.FindFirstObjectByType<CombatMatchRunner>();
            Assert.IsNotNull(runner, "no CombatMatchRunner in Match.unity");
            runner.Configure(null, null, null, IdleInput.Instance);
        }

        public IEnumerator Unload()
        {
            Scene empty = SceneManager.CreateScene("QA_Empty_" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            if (matchScene.IsValid() && matchScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(matchScene);
            GameSession.Reset();
        }
    }
}
