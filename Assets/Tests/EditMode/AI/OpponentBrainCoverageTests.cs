using System.Linq;
using NUnit.Framework;
using RuralRumble.AI;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>QA additions for COMBAT_SPEC CPU AI and D-030 (CPU strike-back while Recovering).</summary>
    public class OpponentBrainCoverageTests
    {
        private const float Dt = 0.02f;

        // D-030: "The CPU keeps a tunable chance (starting at 30%) to strike back while Recovering if the player is
        // in range." At chance 1 a Recovering CPU next to the idle player throws a strike straight from Recovering.
        [Test]
        public void Recovering_CounterChanceCertain_StrikesBackAtPlayerInRange()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.cpuMashRate = 0f;
                t.cpuAttackChance = 0f; t.cpuRecoverCounterChance = 1f;
            });
            h.CpuSource = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, 5);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.Recovering, 2f, Dt);
            h.TickUntil(() => h.MovesStarted.Any(m => m.id == WrestlerId.Cpu), 2f, Dt);
            Assert.AreEqual(MoveId.Strike1, h.MovesStarted.First(m => m.id == WrestlerId.Cpu).move);
            Assert.IsTrue(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.Recovering, CombatState.Striking)),
                "the strike was thrown from Recovering");
        }

        // Counterpart: at chance 0 (and attack chance 0) the Recovering CPU never strikes while Recovering.
        [Test]
        public void Recovering_CounterChanceZero_DoesNotStrikeBack()
        {
            using var h = new MatchHarness(new Vector2(10f, -12f), new Vector2(12f, -12f), configure: t =>
            {
                t.maxHealth = 32f; t.minKnockdownTime = 0.5f; t.maxKnockdownTime = 0.5f; t.cpuMashRate = 0f;
                t.cpuAttackChance = 0f; t.cpuRecoverCounterChance = 0f;
            });
            h.CpuSource = new OpponentBrain(h.Cpu, h.Player, h.Tuning, () => h.Match.ElapsedSeconds, 5);
            h.PlayerInput.PressStrikeOnce();
            h.TickUntil(() => h.Cpu.State == CombatState.Recovering, 2f, Dt);
            h.TickSeconds(0.5f, Dt);
            Assert.IsFalse(h.StateChanges.Contains((WrestlerId.Cpu, CombatState.Recovering, CombatState.Striking)));
        }
    }
}
