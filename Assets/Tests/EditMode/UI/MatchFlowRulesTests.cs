using System;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.UI;

namespace RuralRumble.Tests.EditMode
{
    public class MatchFlowRulesTests
    {
        [Test]
        public void VictoryInBuild_LoadsVictory()
        {
            Assert.AreEqual(SceneId.Victory, MatchFlowRules.NextScene(n => n == SceneNames.Victory));
        }

        [Test]
        public void VictoryMissing_LoadsStart()
        {
            Assert.AreEqual(SceneId.Start, MatchFlowRules.NextScene(n => false));
        }

        [Test]
        public void NullPredicate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MatchFlowRules.NextScene(null));
        }
    }
}
