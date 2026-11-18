using System.Collections;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// D-046 match time hook in Match.unity (ARCHITECTURE 4.2, 3.1): CombatMatchRunner.RequestHitstop /
    /// RequestSlowMotion pause or slow the combat clock and the wrestler Animators; MatchCamera.PunchIn zooms in and
    /// returns. Time.timeScale is never changed.
    /// </summary>
    public class MatchTimeHookSceneTests
    {
        private readonly MatchSceneLoader loader = new MatchSceneLoader();

        [UnityTearDown]
        public IEnumerator UnloadMatch() => loader.Unload();

        [UnityTest]
        public IEnumerator Hitstop_FreezesCombatClockAndAnimators_ThenResumes()
        {
            yield return loader.Load(silenceCpu: true);
            MatchBootstrap boot = MatchBootstrap.Current;
            CombatMatchRunner runner = boot.Runner;
            Assert.IsNotNull(runner, "MatchBootstrap.Runner is null");
            Animator animator = boot.PlayerRoot.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(1f, animator.speed, "idle hook leaves Animator speed at 1");

            runner.RequestHitstop(0.3f);
            yield return null;
            float elapsed = boot.Match.ElapsedSeconds;
            yield return null;
            yield return null;
            Assert.AreEqual(elapsed, boot.Match.ElapsedSeconds, "combat clock (match timer) paused");
            Assert.AreEqual(0f, animator.speed, "wrestler Animator frozen");
            Assert.AreEqual(0f, runner.CombatTimeScale);
            Assert.AreEqual(1f, Time.timeScale, "Time.timeScale untouched");

            yield return new WaitForSecondsRealtime(0.4f);
            yield return null;
            Assert.Greater(boot.Match.ElapsedSeconds, elapsed, "combat clock resumes");
            Assert.AreEqual(1f, animator.speed, "Animator speed back to 1");
        }

        [UnityTest]
        public IEnumerator SlowMotion_ScalesCombatClockAndAnimators()
        {
            yield return loader.Load(silenceCpu: true);
            MatchBootstrap boot = MatchBootstrap.Current;
            Animator animator = boot.CpuRoot.GetComponentInChildren<Animator>();
            boot.Runner.RequestSlowMotion(0.25f, 0.5f);
            yield return null;
            float e0 = boot.Match.ElapsedSeconds;
            float t0 = Time.time;
            for (int i = 0; i < 5; i++) yield return null;
            float combat = boot.Match.ElapsedSeconds - e0, real = Time.time - t0;
            Assert.AreEqual(0.25f * real, combat, 0.25f * real * 0.05f + 1e-4f, "combat time runs at 0.25x");
            Assert.AreEqual(0.25f, animator.speed, 0.01f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return null;
            Assert.AreEqual(1f, boot.Runner.CombatTimeScale);
            Assert.AreEqual(1f, animator.speed);
        }

        [UnityTest]
        public IEnumerator PunchIn_ZoomsInOnPoint_ThenReturnsToDefault()
        {
            yield return loader.Load(silenceCpu: true);
            MatchCamera cam = MatchBootstrap.Current.MatchCamera;
            Assert.IsNotNull(cam, "MatchBootstrap.MatchCamera is null");
            cam.TargetCamera.aspect = 16f / 9f;
            yield return new WaitForSecondsRealtime(1.0f);
            float defaultSize = cam.DefaultOrthographicSize;
            Assert.AreEqual(defaultSize, cam.TargetCamera.orthographicSize, 1e-3f);

            Vector2 point = (Vector2)MatchBootstrap.Current.CpuRoot.position + new Vector2(0f, 1f);
            cam.PunchIn(point, 0.75f, 0.8f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(cam.IsPunchedIn);
            Assert.AreEqual(defaultSize * 0.75f, cam.TargetCamera.orthographicSize, 0.05f, "zoomed in");
            Assert.AreEqual(point.x, cam.transform.position.x, 0.1f, "centered on the point");

            yield return new WaitForSecondsRealtime(3.0f); // hold ends at 0.8 s, then zoomSmoothTime easing
            Assert.IsFalse(cam.IsPunchedIn);
            Assert.AreEqual(defaultSize, cam.TargetCamera.orthographicSize, 1e-3f, "back to the default zoom");
        }
    }
}
