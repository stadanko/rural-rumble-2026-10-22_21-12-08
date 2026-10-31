using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RuralRumble.Tests.PlayMode
{
    /// <summary>
    /// End-to-end combat in the real Match.unity (combat-core slice): the CPU brain fights through the same
    /// rules as the player (COMBAT_SPEC CPU AI), and the player's own keyboard Strike (J, CONTROLS / D-019)
    /// reaches Combat through PlayerInputSource and lands.
    /// </summary>
    public class CombatSceneTests
    {
        private readonly MatchSceneLoader loader = new MatchSceneLoader();

        [UnityTearDown]
        public IEnumerator UnloadMatch() => loader.Unload();

        // COMBAT_SPEC CPU AI example: "Peaux begins outside striking range and enters the Approach state ... When
        // Peaux reaches striking distance, the FSM selects Attack and executes a jab." The player does nothing.
        [UnityTest]
        public IEnumerator CpuBrain_ApproachesIdlePlayer_AndLandsAStrike()
        {
            yield return loader.Load(silenceCpu: false);
            CombatMatch match = MatchBootstrap.Current.Match;
            var hits = new List<HitInfo>();
            match.Events.HitLanded += hits.Add;

            const float timeout = 15f;
            float waited = 0f;
            while (hits.Count == 0 && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsNotEmpty(hits, $"the CPU did not land a strike on an idle player within {timeout} s");
            HitInfo first = hits[0];
            Assert.AreEqual(WrestlerId.Cpu, first.Attacker);
            Assert.AreEqual(WrestlerId.Player, first.Defender);
            Assert.AreEqual(MoveId.Strike1, first.Move, "a combo starts with Strike 1");
            Assert.AreEqual(match.Tuning.DamageFor(MoveId.Strike1), first.Damage, 1e-4f);
            Assert.Less(match.Player.Health, match.Player.MaxHealth, "the hit must cost the player health");
        }

        // CONTROLS: X / J = Strike. The Architect could not confirm the player's own strikes in Play Mode.
        // CPU silenced so the CPU can't interrupt the player's strike.
        [UnityTest]
        public IEnumerator PlayerKeyboard_J_ThrowsStrike1_AndLandsOnTheCpu()
        {
            yield return loader.Load(silenceCpu: true);
            CombatMatch match = MatchBootstrap.Current.Match;
            var moves = new List<(WrestlerId id, MoveId move)>();
            var hits = new List<HitInfo>();
            match.Events.MoveStarted += (id, m, _) => moves.Add((id, m));
            match.Events.HitLanded += hits.Add;

            using (var keyboard = new TestKeyboard())
            {
                // 1. A J tap from Standing starts Strike 1 (out of reach: the CPU is 8.67 units away).
                keyboard.SetPressed("J");
                yield return null;
                yield return null;
                keyboard.ReleaseAll();
                Assert.AreEqual(CombatState.Striking, match.Player.State, "J should put the player in Striking");
                Assert.AreEqual(MoveId.Strike1, match.Player.CurrentMove);
                Assert.Contains((WrestlerId.Player, MoveId.Strike1), moves);
                Assert.IsEmpty(hits, "out of reach: the strike whiffs");

                // Wait for Strike 1 to end and the combo window to close, so the next press is a fresh Strike 1.
                yield return new WaitForSeconds(match.Tuning.strike1Timing.Total + match.Tuning.comboWindow + 0.2f);
                Assert.AreEqual(CombatState.Standing, match.Player.State);

                // 2. Walk right into the CPU (blocked at contact, D-027), then J lands.
                keyboard.SetPressed("D");
                yield return new WaitForSeconds(3.2f);
                keyboard.ReleaseAll();
                yield return null;
                yield return null;
                float gap = match.Cpu.Position.x - match.Player.Position.x;
                Assert.That(gap, Is.InRange(2f * match.Tuning.feetHalfWidth - 1e-3f, 2f * match.Tuning.feetHalfWidth + 0.05f),
                    "setup: the player should be stopped against the CPU");

                keyboard.SetPressed("J");
                yield return null;
                yield return null;
                keyboard.ReleaseAll();
                yield return new WaitForSeconds(match.Tuning.strike1Timing.Startup + match.Tuning.strike1Timing.Active + 0.1f);
            }

            Assert.AreEqual(1, hits.Count, "the player's Strike 1 should land on the adjacent CPU");
            Assert.AreEqual(WrestlerId.Player, hits[0].Attacker);
            Assert.AreEqual(WrestlerId.Cpu, hits[0].Defender);
            Assert.AreEqual(MoveId.Strike1, hits[0].Move);
            Assert.AreEqual(match.Cpu.MaxHealth - match.Tuning.strike1Damage, match.Cpu.Health, 1e-4f);
        }
    }
}
