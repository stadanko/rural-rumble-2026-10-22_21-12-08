using System.Reflection;
using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.UI;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class VictoryTextTests
    {
        WrestlerDefinition _barry;
        WrestlerDefinition _peaux;

        [SetUp]
        public void SetUp()
        {
            _barry = Make("Barry", new[] { "Hot potato!", "Too easy." });
            _peaux = Make("Peaux", new string[0]);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_barry);
            Object.DestroyImmediate(_peaux);
        }

        static WrestlerDefinition Make(string name, string[] lines)
        {
            var def = ScriptableObject.CreateInstance<WrestlerDefinition>();
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(WrestlerDefinition).GetField("displayName", f).SetValue(def, name);
            typeof(WrestlerDefinition).GetField("victoryLines", f).SetValue(def, lines);
            return def;
        }

        [Test]
        public void PlayerWin()
        {
            var c = VictoryText.Build(new MatchResult(WrestlerId.Player, WrestlerId.Cpu, 83f), _barry, _peaux, 1);
            Assert.AreEqual("Victory! Barry knocked out Peaux in 1:23", c.Headline);
            Assert.AreEqual("Congratulations!", c.Outcome);
            Assert.AreEqual(VictoryOutcomeKind.Win, c.Kind);
            Assert.AreEqual("\"Too easy.\"", c.Quote);
            Assert.AreSame(_barry, c.FirstCard);
            Assert.IsNull(c.SecondCard);
        }

        [Test]
        public void CpuWin()
        {
            var c = VictoryText.Build(new MatchResult(WrestlerId.Cpu, WrestlerId.Player, 5f), _barry, _peaux, 0);
            Assert.AreEqual("Victory! Peaux knocked out Barry in 0:05", c.Headline);
            Assert.AreEqual("Game Over!", c.Outcome);
            Assert.AreEqual(VictoryOutcomeKind.Lose, c.Kind);
            Assert.AreSame(_peaux, c.FirstCard);
            Assert.IsNull(c.SecondCard);
        }

        [Test]
        public void Draw()
        {
            var c = VictoryText.Build(MatchResult.Draw(125f), _barry, _peaux, 0);
            Assert.AreEqual("Double Knockout! Barry and Peaux knocked each other out in 2:05", c.Headline);
            Assert.AreEqual("Nobody Wins!", c.Outcome);
            Assert.AreEqual(VictoryOutcomeKind.Draw, c.Kind);
            Assert.IsNull(c.Quote);
            Assert.AreSame(_barry, c.FirstCard);
            Assert.AreSame(_peaux, c.SecondCard);
        }

        [Test]
        public void EmptyLinesGiveNoQuote()
        {
            var c = VictoryText.Build(new MatchResult(WrestlerId.Cpu, WrestlerId.Player, 10f), _barry, _peaux, 7);
            Assert.IsNull(c.Quote);
        }

        [Test]
        public void LineRollWrapsByModulo()
        {
            var c = VictoryText.Build(new MatchResult(WrestlerId.Player, WrestlerId.Cpu, 10f), _barry, _peaux, 4);
            Assert.AreEqual("\"Hot potato!\"", c.Quote);
        }

        [Test]
        public void NoMatchUsesPreviewResult()
        {
            var c = VictoryText.Build(default, _barry, _peaux, 0);
            Assert.AreEqual("Victory! Barry knocked out Peaux in 1:23", c.Headline);
            Assert.AreEqual("Congratulations!", c.Outcome);
        }
    }
}
