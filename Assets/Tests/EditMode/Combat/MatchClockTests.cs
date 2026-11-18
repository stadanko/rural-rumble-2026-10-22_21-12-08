using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// D-046 match time hook (ARCHITECTURE 4.2): MatchClock turns real seconds into combat seconds for hitstop and
    /// slow motion. Idle it must pass dt through unchanged; combat rules see only fewer seconds.
    /// </summary>
    public class MatchClockTests
    {
        private const float Eps = 1e-6f;

        [Test]
        public void Idle_ReturnsRealDtUnchanged_ScaleOne()
        {
            var clock = new MatchClock();
            Assert.IsTrue(clock.IsIdle);
            Assert.AreEqual(1f, clock.CurrentScale);
            foreach (float dt in new[] { 0f, 0.0166667f, 0.02f, 0.1f, 0.5f })
                Assert.AreEqual(dt, clock.Advance(dt), "idle Advance must be the identity (bit-exact)");
        }

        [Test]
        public void Hitstop_FreezesForRequestedRealTime_ThenRunsNormally()
        {
            var clock = new MatchClock();
            clock.RequestHitstop(0.05f);
            Assert.AreEqual(0f, clock.CurrentScale);
            Assert.AreEqual(0f, clock.Advance(0.02f), Eps);
            Assert.AreEqual(0f, clock.Advance(0.02f), Eps);
            // 0.01 s of hitstop left: the frame's other 0.01 s runs at 1.
            Assert.AreEqual(0.01f, clock.Advance(0.02f), Eps);
            Assert.IsTrue(clock.IsIdle);
            Assert.AreEqual(0.02f, clock.Advance(0.02f));
        }

        [Test]
        public void Hitstop_Overlapping_TakesMax_NoStacking()
        {
            var clock = new MatchClock();
            clock.RequestHitstop(0.09f);
            clock.RequestHitstop(0.05f);
            Assert.AreEqual(0.09f, clock.HitstopRemaining, Eps, "a shorter request does not shorten or add");
            clock.Advance(0.04f);
            clock.RequestHitstop(0.12f);
            Assert.AreEqual(0.12f, clock.HitstopRemaining, Eps, "a longer request extends to its own length, not the sum");
        }

        [Test]
        public void SlowMotion_ScalesForRequestedRealTime_ThenNormal()
        {
            var clock = new MatchClock();
            clock.RequestSlowMotion(0.25f, 1.0f);
            Assert.AreEqual(0.25f, clock.CurrentScale, Eps);
            float combat = 0f;
            for (int i = 0; i < 8; i++) combat += clock.Advance(0.125f); // 1.0 s real (binary-exact steps)
            Assert.AreEqual(0.25f, combat, 1e-6f);
            Assert.IsTrue(clock.IsIdle);
            Assert.AreEqual(1f, clock.CurrentScale);
            Assert.AreEqual(0.02f, clock.Advance(0.02f));
        }

        [Test]
        public void SlowMotion_FrameCrossingTheEnd_IsSplit()
        {
            var clock = new MatchClock();
            clock.RequestSlowMotion(0.5f, 0.01f);
            Assert.AreEqual(0.01f * 0.5f + 0.01f, clock.Advance(0.02f), Eps);
        }

        [Test]
        public void SlowMotion_NewRequestReplacesCurrent()
        {
            var clock = new MatchClock();
            clock.RequestSlowMotion(0.25f, 1f);
            clock.RequestSlowMotion(0.5f, 0.2f);
            Assert.AreEqual(0.5f, clock.SlowMotionScale, Eps);
            Assert.AreEqual(0.2f, clock.SlowMotionRemaining, Eps);
        }

        [Test]
        public void HitstopDuringSlowMotion_FreezesFirst_BothCountRealTime()
        {
            var clock = new MatchClock();
            clock.RequestSlowMotion(0.25f, 0.1f);
            clock.RequestHitstop(0.04f);
            // [0, 0.04] frozen, [0.04, 0.1] at 0.25, [0.1, 0.12] at 1.
            Assert.AreEqual(0.06f * 0.25f + 0.02f, clock.Advance(0.12f), Eps);
            Assert.IsTrue(clock.IsIdle);
        }

        [Test]
        public void InvalidRequests_AreIgnored_ScaleClamped()
        {
            var clock = new MatchClock();
            clock.RequestHitstop(0f);
            clock.RequestHitstop(-1f);
            clock.RequestHitstop(float.NaN);
            clock.RequestSlowMotion(0.5f, 0f);
            clock.RequestSlowMotion(float.NaN, 1f);
            Assert.IsTrue(clock.IsIdle);
            clock.RequestSlowMotion(3f, 1f);
            Assert.AreEqual(1f, clock.SlowMotionScale, "scale is clamped to 0..1");
            clock.Cancel();
            Assert.IsTrue(clock.IsIdle);
            Assert.AreEqual(0.02f, clock.Advance(0.02f));
        }

        // ---------------------------------------------------------------- with a real CombatMatch

        /// <summary>Ticks the harness the way CombatMatchRunner does: skip fully frozen frames, else Tick(combat dt).</summary>
        private static void RunFrames(MatchHarness h, MatchClock clock, float realSeconds, float frameDt = MatchHarness.Dt)
        {
            int frames = Mathf.CeilToInt(realSeconds / frameDt - 1e-4f);
            for (int i = 0; i < frames; i++)
            {
                bool active = !clock.IsIdle;
                float dt = clock.Advance(frameDt);
                if (active && dt <= 0f) continue;
                h.Tick(1, dt);
            }
        }

        [Test]
        public void Hitstop_PausesMatchTimerAndCombat_TimingsUnchangedInCombatTime()
        {
            // Baseline: no hook. Combat time at which Strike 1 lands.
            float baseline;
            using (var b = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f)))
            {
                b.PlayerInput.PressStrikeOnce();
                b.TickUntil(() => b.Hits.Count > 0, 1f);
                baseline = b.Match.ElapsedSeconds;
            }

            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f));
            var clock = new MatchClock();
            h.PlayerInput.PressStrikeOnce();
            h.Tick(1);
            Assert.AreEqual(MoveId.Strike1, h.Player.CurrentMove);
            float elapsed = h.Match.ElapsedSeconds;
            MovePhase phase = h.Player.Phase;

            clock.RequestHitstop(0.1f);
            RunFrames(h, clock, 0.1f);
            Assert.AreEqual(elapsed, h.Match.ElapsedSeconds, "the match timer is paused during hitstop");
            Assert.AreEqual(phase, h.Player.Phase, "no combat progress during hitstop");
            Assert.AreEqual(0, h.Hits.Count);

            // Afterwards the strike lands at the same combat time as without the hitstop.
            h.TickUntil(() => h.Hits.Count > 0, 1f);
            Assert.AreEqual(baseline, h.Match.ElapsedSeconds, 1e-5f);
        }

        [Test]
        public void SlowMotionKnockout_SlideStillFinishes_MatchEndedOnce()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t => t.maxHealth = 3f);
            var clock = new MatchClock();
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Match.IsOver, 1f);
            Assert.AreEqual(1, h.MatchEnds.Count);
            Vector2 ko = h.Cpu.Position;

            // D-046 starting point: 0.25x for 1.0 s real, after a KO hitstop.
            clock.RequestHitstop(0.12f);
            clock.RequestSlowMotion(0.25f, 1.0f);
            RunFrames(h, clock, 1.0f);
            Assert.Less(h.Cpu.Position.x, ko.x + h.Tuning.knockdownSlideDistance - 1e-3f,
                "0.22 s of combat time is less than the slide duration, so it is still sliding");
            Assert.Greater(h.Cpu.Position.x, ko.x, "the slide advances during slow motion");

            RunFrames(h, clock, h.Tuning.knockdownSlideDuration + 0.1f);
            Assert.AreEqual(ko.x + h.Tuning.knockdownSlideDistance, h.Cpu.Position.x, 1e-4f, "slide finishes");
            Assert.IsTrue(h.Match.IsOver);
            Assert.AreEqual(1, h.MatchEnds.Count, "MatchEnded is raised once");
        }
    }
}
