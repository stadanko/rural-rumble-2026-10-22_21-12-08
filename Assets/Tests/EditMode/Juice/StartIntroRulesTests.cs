using NUnit.Framework;
using RuralRumble.Juice;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class StartIntroRulesTests
    {
        [Test]
        public void Reveal_StartsDim()
        {
            var r = new StartReveal(0.25f, 0.5f, 0f);
            Assert.AreEqual(StartReveal.Phase.Dim, r.Current);
            Assert.AreEqual(0.25f, r.Brightness, 1e-6f);
            r.Advance(100f);
            Assert.AreEqual(0.25f, r.Brightness, 1e-6f, "no failsafe: stays dim until the logo lands");
        }

        [Test]
        public void Landing_RampsToFull_WithSlam()
        {
            var r = new StartReveal(0.25f, 0.5f, 0f);
            Assert.IsTrue(r.Land(false));
            Assert.AreEqual(StartReveal.Phase.Ramping, r.Current);
            Assert.AreEqual(0.25f, r.Brightness, 1e-6f);

            r.Advance(0.25f);
            float mid = r.Brightness;
            Assert.Greater(mid, 0.25f + 0.75f * 0.5f, "ease-out: past the halfway brightness at half time");
            Assert.Less(mid, 1f);

            r.Advance(0.25f);
            Assert.AreEqual(StartReveal.Phase.Full, r.Current);
            Assert.AreEqual(1f, r.Brightness, 1e-6f);
        }

        [Test]
        public void Skip_SnapsToFull_WithoutSlam()
        {
            var r = new StartReveal(0.25f, 0.5f, 0f);
            Assert.IsFalse(r.Land(true));
            Assert.AreEqual(StartReveal.Phase.Full, r.Current);
            Assert.AreEqual(1f, r.Brightness, 1e-6f);
        }

        [Test]
        public void Skip_DuringRamp_SnapsToFull()
        {
            var r = new StartReveal(0.25f, 0.5f, 0f);
            r.Land(false);
            r.Advance(0.1f);
            Assert.IsFalse(r.Land(true));
            Assert.AreEqual(1f, r.Brightness, 1e-6f);
        }

        [Test]
        public void SecondLanding_DoesNotSlamAgain()
        {
            var r = new StartReveal(0.25f, 0.5f, 0f);
            Assert.IsTrue(r.Land(false));
            Assert.IsFalse(r.Land(false));
            r.Advance(1f);
            Assert.IsFalse(r.Land(false));
        }

        [Test]
        public void ZeroRamp_SnapsOnLanding()
        {
            var r = new StartReveal(0.25f, 0f, 0f);
            Assert.IsTrue(r.Land(false));
            Assert.AreEqual(1f, r.Brightness, 1e-6f);
        }

        [Test]
        public void Failsafe_RampsUpWithoutLanding_AndLaterLandingDoesNotSlam()
        {
            var r = new StartReveal(0.25f, 0.5f, 8f);
            r.Advance(7.9f);
            Assert.AreEqual(StartReveal.Phase.Dim, r.Current);
            r.Advance(0.2f);
            Assert.AreEqual(StartReveal.Phase.Ramping, r.Current);
            Assert.IsFalse(r.Land(false));
            r.Advance(0.5f);
            Assert.AreEqual(1f, r.Brightness, 1e-6f);
        }

        [Test]
        public void EaseOut_Endpoints_AndMonotonic()
        {
            Assert.AreEqual(0f, StartIntroRules.EaseOut(0f), 1e-6f);
            Assert.AreEqual(1f, StartIntroRules.EaseOut(1f), 1e-6f);
            Assert.AreEqual(1f, StartIntroRules.EaseOut(2f), 1e-6f);
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float v = StartIntroRules.EaseOut(i / 20f);
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        [TestCase(0f, 0.25f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 1f)]
        public void BrightnessAt_Endpoints(float elapsed, float expected)
        {
            Assert.AreEqual(expected, StartIntroRules.BrightnessAt(elapsed, 0.5f, 0.25f), 1e-6f);
        }

        [Test]
        public void SpawnFraction_PicksSideAndStaysInRange()
        {
            Assert.AreEqual(-0.35f, StartIntroRules.SpawnFraction(0f, 0f, 0.35f), 1e-6f);
            Assert.AreEqual(-1f, StartIntroRules.SpawnFraction(0.2f, 1f, 0.35f), 1e-6f);
            Assert.AreEqual(0.35f, StartIntroRules.SpawnFraction(1f, 0f, 0.35f), 1e-6f);
            Assert.AreEqual(1f, StartIntroRules.SpawnFraction(0.9f, 1f, 0.35f), 1e-6f);
        }

        [Test]
        public void DustVelocity_MovesAwayFromCentre_FasterAtEnds()
        {
            Vector2 left = StartIntroRules.DustVelocity(-1f, 10f, 2f, 0.5f, 1f);
            Vector2 right = StartIntroRules.DustVelocity(1f, 10f, 2f, 0.5f, 1f);
            Vector2 inner = StartIntroRules.DustVelocity(0.2f, 10f, 2f, 0.5f, 1f);
            Assert.Less(left.x, 0f);
            Assert.Greater(right.x, 0f);
            Assert.AreEqual(-left.x, right.x, 1e-5f);
            Assert.Less(inner.x, right.x);
            Assert.AreEqual(2f, right.y, 1e-6f);
            Assert.Greater(StartIntroRules.DustVelocity(1f, 10f, 2f, 0.5f, 0f).y, 0f);
        }

        [Test]
        public void PanelToScreen_ScalesAndFlipsY()
        {
            // 1920x1080 panel on a 960x540 screen: half scale, y flipped.
            Vector2 s = StartIntroRules.PanelToScreen(new Vector2(960f, 1080f), new Vector2(1920f, 1080f), new Vector2(960f, 540f));
            Assert.AreEqual(480f, s.x, 1e-4f);
            Assert.AreEqual(0f, s.y, 1e-4f);
            Vector2 top = StartIntroRules.PanelToScreen(Vector2.zero, new Vector2(1920f, 1080f), new Vector2(960f, 540f));
            Assert.AreEqual(540f, top.y, 1e-4f);
        }
    }
}
