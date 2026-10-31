using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// One match's combat rules (ARCHITECTURE 4.2, 4.5). Plain C#: no Time, Input or scene access.
    /// Both wrestlers run the same rules; only their input source differs.
    /// </summary>
    public sealed class CombatMatch
    {
        private readonly CombatWrestler player;
        private readonly CombatWrestler cpu;

        public CombatMatch(CombatTuning tuning, ArenaGeometry arena, Vector2 playerStartFeet, Vector2 cpuStartFeet)
        {
            Tuning = tuning;
            Arena = arena ?? ArenaGeometry.Unbounded();
            Events = new CombatEvents();

            // Starting facing: each wrestler faces the other.
            int playerFacing = cpuStartFeet.x >= playerStartFeet.x ? 1 : -1;
            player = new CombatWrestler(WrestlerId.Player, tuning, Events, playerStartFeet, playerFacing);
            cpu = new CombatWrestler(WrestlerId.Cpu, tuning, Events, cpuStartFeet, -playerFacing);
            player.PlaceAt(Arena.ClampToBounds(playerStartFeet, player.FeetHalfExtents));
            cpu.PlaceAt(Arena.ClampToBounds(cpuStartFeet, cpu.FeetHalfExtents));
        }

        public CombatTuning Tuning { get; }
        public ArenaGeometry Arena { get; }
        public CombatEvents Events { get; }
        public IWrestlerView Player => player;
        public IWrestlerView Cpu => cpu;
        /// <summary>Seconds of combat simulated so far (sum of clamped dt).</summary>
        public float ElapsedSeconds { get; private set; }

        public IWrestlerView Get(WrestlerId id) => id == WrestlerId.Player ? player : cpu;

        /// <summary>
        /// Advances the match. dt is clamped to [0, tuning.maxTickStep] (D-021). Each intent is the single
        /// Read() of that wrestler's input for this tick.
        /// Wrestlers do not block each other (movement slice; needs design confirmation).
        /// </summary>
        public void Tick(float dt, WrestlerIntent playerIntent, WrestlerIntent cpuIntent)
        {
            dt = Mathf.Clamp(dt, 0f, Tuning.maxTickStep);
            ElapsedSeconds += dt;
            player.Tick(dt, playerIntent, Arena);
            cpu.Tick(dt, cpuIntent, Arena);
        }
    }
}
