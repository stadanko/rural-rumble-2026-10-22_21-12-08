using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Combat publishes; presentation, UI, AI and Juice subscribe (ARCHITECTURE 4.2). One instance per match.
    /// All events fire inside <see cref="CombatMatch.Tick"/>. Subscribers never change combat state.
    /// Combat-core slice contracts: MoveStarted, MovePhaseChanged, HitLanded, ObstacleHit, HealthChanged,
    /// KnockedDown and MatchEnded are declared here; Combat raises them as it implements each rule.
    /// Lock-up slice: PairedMoveStarted (ARCHITECTURE 6).
    /// </summary>
    public sealed class CombatEvents
    {
        /// <summary>(wrestler, from, to). Fires once per transition. Never fires for a same-state restart
        /// (e.g. Struck hit again, D-024); use <see cref="HitLanded"/> for that.</summary>
        public event Action<WrestlerId, CombatState, CombatState> StateChanged;

        /// <summary>
        /// (wrestler, new Facing). Fires when the 8-way facing vector changes. Read the wrestler's
        /// <see cref="IWrestlerView.HorizontalFacing"/> for the sprite flip.
        /// </summary>
        public event Action<WrestlerId, Vector2> FacingChanged;

        /// <summary>
        /// (wrestler, move, timing). A move starts in its Startup phase. The timing is the tuned value read
        /// at this moment, so presentation can scale the clip so its contact frame lands at Startup.
        /// </summary>
        public event Action<WrestlerId, MoveId, MoveTiming> MoveStarted;

        /// <summary>(wrestler, move, new phase). Startup -> Active -> Recovery -> None. A move cut short
        /// (the attacker is hit) goes straight to None.</summary>
        public event Action<WrestlerId, MoveId, MovePhase> MovePhaseChanged;

        /// <summary>
        /// A two-wrestler synchronized move started (ARCHITECTURE 6): lock-up, break lock-up (lock-up slice
        /// step 1), bodyslam and Irish Whip (later steps). Both positions are already snapped to the pair
        /// anchor and both states set (Lock Up / Locked Up). Paired moves raise this instead of
        /// <see cref="MoveStarted"/>; <see cref="MovePhaseChanged"/> still fires for each wrestler.
        /// A lock-up whiff (D-031) is a one-wrestler move and raises <see cref="MoveStarted"/> (LockUp) only.
        /// </summary>
        public event Action<PairedMoveInfo> PairedMoveStarted;

        /// <summary>A move connected: attacker, defender, move, damage, contact point. Fires on every hit,
        /// including a hit that restarts Struck.</summary>
        public event Action<HitInfo> HitLanded;

        /// <summary>(wrestler, contact point). A running or whipped wrestler hit a damaging arena object
        /// (truck or pool, D-027). Not raised for the back wall or the arena ends.</summary>
        public event Action<WrestlerId, Vector2> ObstacleHit;

        /// <summary>(wrestler, new health). Damage and Recovering regeneration.</summary>
        public event Action<WrestlerId, float> HealthChanged;

        /// <summary>(wrestler, knockdown duration in seconds at the moment of the knockdown, before mashing).</summary>
        public event Action<WrestlerId, float> KnockedDown;

        /// <summary>A knockout ended the match. Raised once; the match is frozen afterwards.</summary>
        public event Action<MatchResult> MatchEnded;

        internal void RaiseStateChanged(WrestlerId id, CombatState from, CombatState to) => StateChanged?.Invoke(id, from, to);
        internal void RaiseFacingChanged(WrestlerId id, Vector2 facing) => FacingChanged?.Invoke(id, facing);
        internal void RaiseMoveStarted(WrestlerId id, MoveId move, MoveTiming timing) => MoveStarted?.Invoke(id, move, timing);
        internal void RaiseMovePhaseChanged(WrestlerId id, MoveId move, MovePhase phase) => MovePhaseChanged?.Invoke(id, move, phase);
        internal void RaisePairedMoveStarted(in PairedMoveInfo info) => PairedMoveStarted?.Invoke(info);
        internal void RaiseHitLanded(in HitInfo hit) => HitLanded?.Invoke(hit);
        internal void RaiseObstacleHit(WrestlerId id, Vector2 contactPoint) => ObstacleHit?.Invoke(id, contactPoint);
        internal void RaiseHealthChanged(WrestlerId id, float health) => HealthChanged?.Invoke(id, health);
        internal void RaiseKnockedDown(WrestlerId id, float duration) => KnockedDown?.Invoke(id, duration);
        internal void RaiseMatchEnded(in MatchResult result) => MatchEnded?.Invoke(result);
    }
}
