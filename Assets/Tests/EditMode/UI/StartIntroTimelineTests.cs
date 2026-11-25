using System;
using NUnit.Framework;
using RuralRumble.UI;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class StartIntroTimelineTests
    {
        const float Eps = 1e-4f;

        // Starting points: delay 0.3, fall 0.45 (impact 0.75), bounce 0.25, shake 0.2, prompt +0.6 (1.35).
        static StartIntroTimeline Defaults() => new StartIntroTimeline();

        static StartIntroSequence Sequence(float inputDelay = 0.2f) => new StartIntroSequence(Defaults(), inputDelay);

        [Test]
        public void Defaults_AreTheStartingPoints()
        {
            var t = Defaults();
            Assert.AreEqual(0.3f, t.startDelay, Eps);
            Assert.AreEqual(0.45f, t.fallDuration, Eps);
            Assert.AreEqual(0.25f, t.bounceDuration, Eps);
            Assert.AreEqual(0.2f, t.shakeDuration, Eps);
            Assert.AreEqual(0.6f, t.promptDelay, Eps);
            Assert.AreEqual(0.75f, t.ImpactTime, Eps);
            Assert.AreEqual(1.35f, t.PromptTime, Eps);
        }

        [TestCase(0f, StartIntroPhase.Waiting)]
        [TestCase(0.29f, StartIntroPhase.Waiting)]
        [TestCase(0.3f, StartIntroPhase.Falling)]
        [TestCase(0.74f, StartIntroPhase.Falling)]
        [TestCase(0.75f, StartIntroPhase.Bouncing)]
        [TestCase(0.99f, StartIntroPhase.Bouncing)]
        [TestCase(1.0f, StartIntroPhase.Revealing)]
        [TestCase(1.34f, StartIntroPhase.Revealing)]
        [TestCase(1.35f, StartIntroPhase.Ready)]
        [TestCase(10f, StartIntroPhase.Ready)]
        public void PhaseAt_FollowsTheTimeline(float time, StartIntroPhase expected)
        {
            Assert.AreEqual(expected, Defaults().PhaseAt(time));
        }

        [Test]
        public void FallProgress_StartsAbove_EasesIn_EndsAtRest()
        {
            var t = Defaults();
            Assert.AreEqual(0f, t.FallProgress(0f), Eps);
            Assert.AreEqual(0f, t.FallProgress(0.3f), Eps);
            // Ease-in: the first half of the time covers well under half the distance (quadratic: a quarter).
            Assert.AreEqual(0.25f, t.FallProgress(0.3f + 0.225f), Eps);
            Assert.AreEqual(1f, t.FallProgress(0.75f), Eps);
            Assert.AreEqual(1f, t.FallProgress(5f), Eps);

            // Accelerating: each equal time slice covers more distance than the one before.
            float prevStep = 0f, prev = 0f;
            for (int i = 1; i <= 9; i++)
            {
                float p = t.FallProgress(0.3f + 0.05f * i);
                Assert.Greater(p - prev, prevStep);
                prevStep = p - prev;
                prev = p;
            }
        }

        [Test]
        public void Squash_WiderShorterOnImpact_OvershootsThenSettles()
        {
            var t = Defaults();
            Assert.AreEqual(0f, t.SquashAt(0.74f), Eps);
            var impact = t.PoseAt(t.ImpactTime);
            Assert.Greater(impact.Scale.x, 1.1f, "wider on impact");
            Assert.Less(impact.Scale.y, 0.9f, "shorter on impact");
            // 40% of the bounce: overshoot the other way (taller, narrower).
            var overshoot = t.PoseAt(t.ImpactTime + 0.4f * t.bounceDuration);
            Assert.Less(overshoot.Scale.x, 1f);
            Assert.Greater(overshoot.Scale.y, 1f);
            Assert.Less(Mathf.Abs(overshoot.Scale.x - 1f), Mathf.Abs(impact.Scale.x - 1f), "overshoot is smaller than the squash");
            var settled = t.PoseAt(t.ImpactTime + t.bounceDuration);
            Assert.AreEqual(1f, settled.Scale.x, Eps);
            Assert.AreEqual(1f, settled.Scale.y, Eps);
        }

        [Test]
        public void Shake_OnlyForShakeDuration_AfterImpact()
        {
            var t = Defaults();
            Assert.AreEqual(Vector2.zero, t.ShakeAt(0.7f));
            Assert.Greater(t.ShakeAt(t.ImpactTime + 0.03f).magnitude, 1f);
            Assert.LessOrEqual(t.ShakeAt(t.ImpactTime + 0.03f).magnitude, t.shakeAmplitude * 1.2f);
            Assert.Less(t.ShakeAt(t.ImpactTime + t.shakeDuration).magnitude, 0.01f);
            Assert.AreEqual(Vector2.zero, t.ShakeAt(t.ImpactTime + t.shakeDuration + 0.01f));
        }

        [Test]
        public void Pose_AboveScreenBeforeFall_AtRestAfterSettle()
        {
            var t = Defaults();
            Assert.AreEqual(1f, t.PoseAt(0f).OffsetFraction, Eps);
            var rest = t.PoseAt(2f);
            Assert.AreEqual(0f, rest.OffsetFraction, Eps);
            Assert.AreEqual(Vector2.one, rest.Scale);
            Assert.AreEqual(Vector2.zero, rest.Shake);
        }

        [Test]
        public void ZeroDurations_DoNotDivideByZero()
        {
            var t = new StartIntroTimeline { startDelay = 0f, fallDuration = 0f, bounceDuration = 0f, shakeDuration = 0f, promptDelay = 0f };
            Assert.AreEqual(1f, t.FallProgress(0f), Eps);
            Assert.AreEqual(0f, t.SquashAt(0f), Eps);
            Assert.AreEqual(Vector2.zero, t.ShakeAt(0f));
            Assert.AreEqual(StartIntroPhase.Ready, t.PhaseAt(0f));
        }

        [Test]
        public void NullTimeline_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new StartIntroSequence(null, 0.2f));
        }

        [Test]
        public void Uninterrupted_RaisesLandedOnceWithSlamAtImpact_ThenShowsPrompt()
        {
            var s = Sequence();
            int landed = 0, slams = 0, prompts = 0;
            float landedAt = -1f, promptAt = -1f;
            for (int i = 0; i < 300; i++) // 5 s at 60 fps
            {
                var step = s.Advance(1f / 60f);
                if (step.RaiseLanded) { landed++; landedAt = s.Elapsed; Assert.IsFalse(step.LandedSkipped); }
                if (step.PlaySlam) { slams++; Assert.IsTrue(step.RaiseLanded, "slam with the landing, not after the bounce"); }
                if (step.ShowPrompt) { prompts++; promptAt = s.Elapsed; }
                Assert.IsFalse(step.StartGame);
            }
            Assert.AreEqual(1, landed);
            Assert.AreEqual(1, slams);
            Assert.AreEqual(1, prompts);
            Assert.AreEqual(0.75f, landedAt, 1f / 60f + Eps);
            Assert.AreEqual(1.35f, promptAt, 1f / 60f + Eps);
            Assert.AreEqual(StartIntroPhase.Ready, s.Phase);
        }

        [Test]
        public void PromptNotVisible_UntilPromptTime()
        {
            var s = Sequence();
            s.Advance(1.3f);
            Assert.IsTrue(s.HasLanded);
            Assert.IsFalse(s.PromptVisible);
            Assert.IsTrue(s.Advance(0.06f).ShowPrompt);
        }

        [Test]
        public void PressInsideInputDelay_IsIgnored()
        {
            var s = Sequence();
            s.Advance(0.1f);
            var step = s.Press();
            Assert.IsFalse(step.RaiseLanded || step.ShowPrompt || step.StartGame);
            Assert.IsFalse(s.Skipped);
        }

        [Test]
        public void PressDuringFall_Skips_RaisesLandedSkipped_NoSlam_ShowsPrompt_DoesNotStart()
        {
            var s = Sequence();
            s.Advance(0.5f);
            var step = s.Press();
            Assert.IsTrue(step.RaiseLanded);
            Assert.IsTrue(step.LandedSkipped);
            Assert.IsFalse(step.PlaySlam);
            Assert.IsTrue(step.ShowPrompt);
            Assert.IsFalse(step.StartGame);
            Assert.IsTrue(s.Skipped);
            Assert.AreEqual(StartIntroPhase.Ready, s.Phase);

            var pose = s.Pose;
            Assert.AreEqual(0f, pose.OffsetFraction, Eps);
            Assert.AreEqual(Vector2.one, pose.Scale);
            Assert.AreEqual(Vector2.zero, pose.Shake);

            // Time passing never raises landed, slam or prompt again.
            for (int i = 0; i < 120; i++)
            {
                var later = s.Advance(1f / 60f);
                Assert.IsFalse(later.RaiseLanded || later.PlaySlam || later.ShowPrompt);
            }
        }

        [Test]
        public void PressDuringBounce_SnapsToRest_DoesNotRaiseLandedAgain()
        {
            var s = Sequence();
            Assert.IsTrue(s.Advance(0.8f).RaiseLanded);
            var step = s.Press();
            Assert.IsFalse(step.RaiseLanded);
            Assert.IsFalse(step.PlaySlam);
            Assert.IsTrue(step.ShowPrompt);
            Assert.IsFalse(step.StartGame);
            Assert.AreEqual(Vector2.one, s.Pose.Scale);
        }

        [Test]
        public void PressAfterSkip_StartsGame_OnlyOnce_AfterInputDelay()
        {
            var s = Sequence();
            s.Advance(0.5f);
            s.Press(); // skip
            Assert.IsFalse(s.Press().StartGame, "a second button in the same frame does not also start");
            s.Advance(0.25f);
            Assert.IsTrue(s.Press().StartGame);
            Assert.IsTrue(s.Started);
            Assert.IsFalse(s.Press().StartGame);
        }

        [Test]
        public void PressAfterPromptShownNaturally_StartsGame()
        {
            var s = Sequence();
            s.Advance(1.4f);
            var step = s.Press();
            Assert.IsTrue(step.StartGame);
            Assert.IsFalse(step.RaiseLanded);
        }

        [Test]
        public void NegativeDt_DoesNotRewind()
        {
            var s = Sequence();
            s.Advance(0.5f);
            s.Advance(-1f);
            Assert.AreEqual(0.5f, s.Elapsed, Eps);
        }
    }
}
