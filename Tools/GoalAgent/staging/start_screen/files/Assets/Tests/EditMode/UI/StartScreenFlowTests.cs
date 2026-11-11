using System;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.UI;

namespace RuralRumble.Tests.EditMode
{
    public class StartScreenFlowTests
    {
        [Test]
        public void CharacterSelectInBuild_LoadsCharacterSelect()
        {
            var next = StartScreenFlow.NextScene(name => name == SceneNames.CharacterSelect);
            Assert.AreEqual(SceneId.CharacterSelect, next);
        }

        [Test]
        public void CharacterSelectMissing_LoadsMatch()
        {
            var next = StartScreenFlow.NextScene(name => name == SceneNames.Match);
            Assert.AreEqual(SceneId.Match, next);
        }

        [Test]
        public void NullPredicate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StartScreenFlow.NextScene(null));
        }
    }
}
