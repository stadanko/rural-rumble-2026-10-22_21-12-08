using NUnit.Framework;
using RuralRumble.Core;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>Architect's asmdef sanity check: the test assembly sees Core. QA owns real tests.</summary>
    public class AssemblySanityTests
    {
        [Test]
        public void GameSession_Reset_ClearsSetup()
        {
            GameSession.Setup = new MatchSetup(null, null);
            Assert.IsTrue(GameSession.HasSetup);
            GameSession.Reset();
            Assert.IsFalse(GameSession.HasSetup);
        }
    }
}
