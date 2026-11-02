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

        public readonly List<HitInfo> Hits = new List<HitInfo>();
        public readonly List<(WrestlerId id, float health)> HealthChanges = new List<(WrestlerId, float)>();
        public readonly List<(WrestlerId id, float duration)> KnockDowns = new List<(WrestlerId, float)>();
        public readonly List<MatchResult> MatchEnds = new List<MatchResult>();
        public readonly List<(WrestlerId id, MoveId move, MoveTiming timing)> MovesStarted = new List<(WrestlerId, MoveId, MoveTiming)>();
        public readonly List<(WrestlerId id, MoveId move, MovePhase phase)> PhaseChanges = new List<(WrestlerId, MoveId, MovePhase)>();
        public readonly List<(WrestlerId id, Vector2 contact)> ObstacleHits = new List<(WrestlerId, Vector2)>();
        public readonly List<PairedMoveInfo> PairedMoves = new List<PairedMoveInfo>();

        /// <summary>When true the CPU reads IdleInput (the slice's stand-in opponent) instead of CpuInput.</summary>
        public bool CpuIdle = true;
        /// <summary>When set (e.g. an OpponentBrain), the CPU reads this instead of CpuInput / IdleInput.</summary>
        public IWrestlerInput CpuSource;

        public MatchHarness(Vector2 playerStart, Vector2? cpuStart = null, ArenaGeometry arena = null,
            Action<CombatTuning> configure = null, Func<float> random01 = null)
        {
            Tuning = ScriptableObject.CreateInstance<CombatTuning>();
            configure?.Invoke(Tuning);
            // Deterministic by default: a fixed-seed source for chance rules (D-028 simultaneous lock-up).
            if (random01 == null)
            {
                var rng = new System.Random(12345);
                random01 = () => (float)rng.NextDouble();
            }
            Match = new CombatMatch(Tuning, arena ?? SliceArena.Build(), playerStart, cpuStart ?? DefaultCpuStart, random01);
            Match.Events.StateChanged += (id, from, to) => StateChanges.Add((id, from, to));
            Match.Events.FacingChanged += (id, f) => FacingChanges.Add((id, f));
            Match.Events.HitLanded += hit => Hits.Add(hit);
            Match.Events.HealthChanged += (id, hp) => HealthChanges.Add((id, hp));
            Match.Events.KnockedDown += (id, d) => KnockDowns.Add((id, d));
            Match.Events.MatchEnded += r => MatchEnds.Add(r);
            Match.Events.MoveStarted += (id, m, t) => MovesStarted.Add((id, m, t));
            Match.Events.MovePhaseChanged += (id, m, ph) => PhaseChanges.Add((id, m, ph));
            Match.Events.ObstacleHit += (id, c) => ObstacleHits.Add((id, c));
            Match.Events.PairedMoveStarted += info => PairedMoves.Add(info);
        }

        public IWrestlerView Player => Match.Player;
        public IWrestlerView Cpu => Match.Cpu;
        /// <summary>The player's combat object, for timers not on IWrestlerView (knockdown, Struck, cooldown).</summary>
        public CombatWrestler PlayerW => (CombatWrestler)Match.Player;
        public CombatWrestler CpuW => (CombatWrestler)Match.Cpu;

        /// <summary>Ticks until <paramref name="until"/> is true (checked after each tick) or maxSeconds pass.
        /// Returns the combat seconds ticked.</summary>
        public float TickUntil(Func<bool> until, float maxSeconds = 10f, float dt = Dt)
        {
            float t = 0f;
            while (t < maxSeconds)
            {
                Tick(1, dt);
                t += dt;
                if (until()) return t;
            }
            Assert.Fail($"condition not reached within {maxSeconds} s");
            return t;
        }
        public Vector2 HalfExtents => new Vector2(Tuning.feetHalfWidth, Tuning.feetHalfDepth);

        public void Tick(int ticks = 1, float dt = Dt, Action<int> afterEachTick = null)
        {
            for (int i = 0; i < ticks; i++)
            {
                WrestlerIntent p = PlayerInput.Read();
                WrestlerIntent c = CpuSource != null ? CpuSource.Read() : CpuIdle ? IdleInput.Instance.Read() : CpuInput.Read();
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
