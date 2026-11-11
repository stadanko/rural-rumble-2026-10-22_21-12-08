using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.UI;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    public class CharacterSelectRulesTests
    {
        [Test]
        public void Move_RightSelectsSecond()
        {
            Assert.AreEqual(1, CharacterSelectRules.Move(0, 0.5f, 2));
        }

        [Test]
        public void Move_LeftSelectsFirst()
        {
            Assert.AreEqual(0, CharacterSelectRules.Move(1, -0.5f, 2));
        }

        [Test]
        public void Move_DoesNotWrap()
        {
            Assert.AreEqual(0, CharacterSelectRules.Move(0, -1f, 2));
            Assert.AreEqual(1, CharacterSelectRules.Move(1, 1f, 2));
        }

        [Test]
        public void Move_SmallInputIgnored()
        {
            Assert.AreEqual(1, CharacterSelectRules.Move(1, 0.3f, 2));
        }

        [Test]
        public void BuildSetup_CpuGetsTheOther()
        {
            var a = ScriptableObject.CreateInstance<WrestlerDefinition>();
            var b = ScriptableObject.CreateInstance<WrestlerDefinition>();
            var list = new[] { a, b };

            var s0 = CharacterSelectRules.BuildSetup(list, 0);
            Assert.AreSame(a, s0.Player);
            Assert.AreSame(b, s0.Cpu);

            var s1 = CharacterSelectRules.BuildSetup(list, 1);
            Assert.AreSame(b, s1.Player);
            Assert.AreSame(a, s1.Cpu);

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }
}
