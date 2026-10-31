using System;
using System.Collections.Generic;
using NUnit.Framework;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Tests.EditMode
{
    /// <summary>
    /// Builds a CombatMatch the way ARCHITECTURE 4.5 describes: ScriptedInput, a fresh CombatTuning
    /// instance (COMBAT_SPEC defaults) and arena rects. Records every event. Dispose destroys the tuning.
    /// </summary>
    public sealed class MatchHarness : IDisposable
    {
        public const float Dt = 0.02f;

        /// <summary>Far right-front corner: out of the way, and the player starts facing it (right).</summary>
        public static readonly Vector2 DefaultCpuStart = new Vector2(25f, -13f);

        public readonly CombatTuning Tuning;
        public readonly CombatMatch Match;
        public readonly ScriptedInput PlayerInput = new ScriptedInput();
        public readonly ScriptedInput CpuInput = new ScriptedInput();

        public readonly List<(WrestlerId id, CombatState from, CombatState to)> StateChanges =
            new List<(WrestlerId, CombatState, CombatState)>();
        public readonly List<(WrestlerId id, Vector2 facing)> FacingChanges = new List<(WrestlerId, Vector2)>();

        /// <summary>When true the CPU reads IdleInput (the slice's stand-in opponent) instead of CpuInput.</summary>
        public bool CpuIdle = true;

        public MatchHarness(Vector2 playerStart, Vector2? cpuStart = null, ArenaGeometry arena = null,
            Action<CombatTuning> configure = null)
        {
            Tuning = ScriptableObject.CreateInstance<CombatTuning>();
            configure?.Invoke(Tuning);
            Match = new CombatMatch(Tuning, arena ?? SliceArena.Build(), playerStart, cpuStart ?? DefaultCpuStart);
            Match.Events.StateChanged += (id, from, to) => StateChanges.Add((id, from, to));
            Match.Events.FacingChanged += (id, f) => FacingChanges.Add((id, f));
        }

        public IWrestlerView Player => Match.Player;
        public IWrestlerView Cpu => Match.Cpu;
        public Vector2 HalfExtents => new Vector2(Tuning.feetHalfWidth, Tuning.feetHalfDepth);

        public void Tick(int ticks = 1, float dt = Dt, Action<int> afterEachTick = null)
        {
            for (int i = 0; i < ticks; i++)
            {
                WrestlerIntent p = PlayerInput.Read();
                WrestlerIntent c = CpuIdle ? IdleInput.Instance.Read() : CpuInput.Read();
                Match.Tick(dt, p, c);
                afterEachTick?.Invoke(i);
            }
        }

        /// <summary>Ticks for roughly <paramref name="seconds"/> of combat time.</summary>
        public void TickSeconds(float seconds, float dt = Dt, Action<int> afterEachTick = null) =>
            Tick(Mathf.CeilToInt(seconds / dt), dt, afterEachTick);

        /// <summary>Asserts after every tick that neither feet box overlaps the truck or pool interior.</summary>
        public void AssertPlayerOutsideObstacles(string context)
        {
            foreach (ArenaRect o in Match.Arena.Obstacles)
                Assert.IsFalse(SliceArena.FeetInsideObstacle(Player.Position, HalfExtents, o),
                    $"{context}: player feet {Player.Position} are inside obstacle {o}");
        }

        public void Dispose()
        {
            if (Tuning != null)
                UnityEngine.Object.DestroyImmediate(Tuning);
        }
    }

    public static class V2Assert
    {
        public static void AreEqual(Vector2 expected, Vector2 actual, float tolerance, string message = null)
        {
            Assert.That(Vector2.Distance(expected, actual), Is.LessThanOrEqualTo(tolerance),
                $"{message} expected {expected:F4} but was {actual:F4}");
        }
    }
}
