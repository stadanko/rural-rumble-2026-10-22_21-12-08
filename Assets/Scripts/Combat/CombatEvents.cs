using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Combat publishes; presentation, UI and Juice subscribe (ARCHITECTURE 4.2). One instance per match.
    /// All events fire inside <see cref="CombatMatch.Tick"/>. Subscribers never change combat state.
    /// Movement slice: only the events movement raises exist. MoveStarted, MovePhaseChanged,
    /// PairedMoveStarted, HitLanded, ObstacleHit, HealthChanged, KnockedDown and MatchEnded come with
    /// the combat-core slice.
    /// </summary>
    public sealed class CombatEvents
    {
        /// <summary>(wrestler, from, to). Fires once per transition.</summary>
        public event Action<WrestlerId, CombatState, CombatState> StateChanged;

        /// <summary>
        /// (wrestler, new Facing). Fires when the 8-way facing vector changes. Read the wrestler's
        /// <see cref="IWrestlerView.HorizontalFacing"/> for the sprite flip.
        /// </summary>
        public event Action<WrestlerId, Vector2> FacingChanged;

        internal void RaiseStateChanged(WrestlerId id, CombatState from, CombatState to) => StateChanged?.Invoke(id, from, to);
        internal void RaiseFacingChanged(WrestlerId id, Vector2 facing) => FacingChanged?.Invoke(id, facing);
    }
}
