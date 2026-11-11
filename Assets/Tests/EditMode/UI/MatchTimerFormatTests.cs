using NUnit.Framework;
using RuralRumble.UI;

namespace RuralRumble.Tests.EditMode
{
    public sealed class MatchTimerFormatTests
    {
        [TestCase(0f, "0:00")]
        [TestCase(0.99f, "0:00")]
        [TestCase(7f, "0:07")]
        [TestCase(7.9f, "0:07")]
        [TestCase(59.99f, "0:59")]
        [TestCase(60f, "1:00")]
        [TestCase(83f, "1:23")]
        [TestCase(725f, "12:05")]
        [TestCase(-5f, "0:00")]
        [TestCase(float.NaN, "0:00")]
        public void Format_ProducesMinutesAndTwoDigitSeconds(float input, string expected)
        {
            Assert.AreEqual(expected, MatchTimerFormat.Format(input));
        }

        [Test]
        public void WholeSeconds_FloorsAndClampsNegative()
        {
            Assert.AreEqual(3, MatchTimerFormat.WholeSeconds(3.7f));
            Assert.AreEqual(0, MatchTimerFormat.WholeSeconds(-1f));
        }
    }
}
