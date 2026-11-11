using NUnit.Framework;
using RuralRumble.UI;

namespace RuralRumble.Tests.EditMode
{
    public class HealthTrailTests
    {
        private const float Hold = 0.4f;
        private const float Drain = 0.5f;

        private static HealthTrail Full()
        {
            var t = new HealthTrail();
            t.Reset(1f);
            return t;
        }

        [Test]
        public void Reset_ShowsNoTrail()
        {
            HealthTrail t = Full();
            Assert.AreEqual(1f, t.Fill, 1e-5f);
            Assert.AreEqual(1f, t.Trail, 1e-5f);
        }

        [Test]
        public void Hit_DropsFillAtOnceAndKeepsTrail()
        {
            HealthTrail t = Full();
            t.SetFill(0.6f, Hold);
            Assert.AreEqual(0.6f, t.Fill, 1e-5f);
            Assert.AreEqual(1f, t.Trail, 1e-5f);
        }

        [Test]
        public void Trail_HoldsDuringHoldTime()
        {
            HealthTrail t = Full();
            t.SetFill(0.6f, Hold);
            t.Tick(Hold - 0.01f, Drain);
            Assert.AreEqual(1f, t.Trail, 1e-5f);
        }

        [Test]
        public void Trail_DrainsLinearlyAfterHold()
        {
            HealthTrail t = Full();
            t.SetFill(0.6f, Hold);
            t.Tick(Hold, Drain);
            t.Tick(Drain * 0.5f, Drain);
            Assert.AreEqual(0.8f, t.Trail, 1e-4f);
        }

        [Test]
        public void Trail_ReachesFillAfterHoldPlusDrain()
        {
            HealthTrail t = Full();
            t.SetFill(0.6f, Hold);
            t.Tick(Hold + Drain + 0.1f, Drain);
            Assert.AreEqual(0.6f, t.Trail, 1e-5f);
        }

        [Test]
        public void SingleLargeTick_SpansHoldAndDrain()
        {
            HealthTrail t = Full();
            t.SetFill(0.5f, Hold);
            t.Tick(Hold + Drain * 0.5f, Drain);
            Assert.AreEqual(0.75f, t.Trail, 1e-4f);
        }

        [Test]
        public void SecondHit_RestartsHold()
        {
            HealthTrail t = Full();
            t.SetFill(0.8f, Hold);
            t.Tick(Hold + 0.1f, Drain);
            float trailBefore = t.Trail;
            t.SetFill(0.6f, Hold);
            t.Tick(Hold - 0.05f, Drain);
            Assert.AreEqual(trailBefore, t.Trail, 1e-5f);
            Assert.AreEqual(0.6f, t.Fill, 1e-5f);
        }

        [Test]
        public void Heal_PullsTrailUpWithFill()
        {
            var t = new HealthTrail();
            t.Reset(0.5f);
            t.SetFill(0.7f, Hold);
            Assert.AreEqual(0.7f, t.Fill, 1e-5f);
            Assert.AreEqual(0.7f, t.Trail, 1e-5f);
        }

        [Test]
        public void ZeroDrain_SnapsAfterHold()
        {
            HealthTrail t = Full();
            t.SetFill(0.2f, Hold);
            t.Tick(Hold + 0.01f, 0f);
            Assert.AreEqual(0.2f, t.Trail, 1e-5f);
        }

        [Test]
        public void ToFraction_ClampsAndHandlesZeroMax()
        {
            Assert.AreEqual(0.5f, HealthTrail.ToFraction(50f, 100f), 1e-5f);
            Assert.AreEqual(0f, HealthTrail.ToFraction(-5f, 100f), 1e-5f);
            Assert.AreEqual(1f, HealthTrail.ToFraction(150f, 100f), 1e-5f);
            Assert.AreEqual(0f, HealthTrail.ToFraction(10f, 0f), 1e-5f);
        }
    }
}
