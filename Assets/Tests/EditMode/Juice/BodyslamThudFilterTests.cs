using NUnit.Framework;
using RuralRumble.Core;
using RuralRumble.Juice;

namespace RuralRumble.Tests.EditMode
{
    public class BodyslamThudFilterTests
    {
        [Test]
        public void Knockdown_WithoutBodyslam_Thuds()
        {
            var f = new BodyslamThudFilter(2f);
            Assert.IsTrue(f.ShouldThudOnKnockdown(WrestlerId.Cpu, 5f));
        }

        [Test]
        public void Knockdown_AfterBodyslamImpact_InsideWindow_IsSilent()
        {
            var f = new BodyslamThudFilter(2f);
            f.NoteBodyslamImpact(WrestlerId.Cpu, 10f);
            Assert.IsFalse(f.ShouldThudOnKnockdown(WrestlerId.Cpu, 11.21f));
        }

        [Test]
        public void Knockdown_AtWindowEdge_IsSilent_JustPast_Thuds()
        {
            var a = new BodyslamThudFilter(2f);
            a.NoteBodyslamImpact(WrestlerId.Cpu, 10f);
            Assert.IsFalse(a.ShouldThudOnKnockdown(WrestlerId.Cpu, 12f));

            var b = new BodyslamThudFilter(2f);
            b.NoteBodyslamImpact(WrestlerId.Cpu, 10f);
            Assert.IsTrue(b.ShouldThudOnKnockdown(WrestlerId.Cpu, 12.01f));
        }

        [Test]
        public void Suppression_IsUsedUp_NextKnockdownThuds()
        {
            var f = new BodyslamThudFilter(2f);
            f.NoteBodyslamImpact(WrestlerId.Player, 10f);
            Assert.IsFalse(f.ShouldThudOnKnockdown(WrestlerId.Player, 11.2f));
            Assert.IsTrue(f.ShouldThudOnKnockdown(WrestlerId.Player, 11.5f));
        }

        [Test]
        public void Suppression_IsPerWrestler()
        {
            var f = new BodyslamThudFilter(2f);
            f.NoteBodyslamImpact(WrestlerId.Cpu, 10f);
            Assert.IsTrue(f.ShouldThudOnKnockdown(WrestlerId.Player, 11.2f));
            Assert.IsFalse(f.ShouldThudOnKnockdown(WrestlerId.Cpu, 11.2f));
        }

        [Test]
        public void Knockout_OnBodyslamImpact_IsSilent_LaterKnockoutThuds()
        {
            var f = new BodyslamThudFilter(2f);
            f.NoteBodyslamImpact(WrestlerId.Cpu, 10f);
            Assert.IsFalse(f.ShouldThudOnKnockout(WrestlerId.Cpu, 10f));
            Assert.IsTrue(f.ShouldThudOnKnockout(WrestlerId.Cpu, 13f));
            Assert.IsTrue(f.ShouldThudOnKnockout(WrestlerId.Player, 10f));
        }
    }
}
