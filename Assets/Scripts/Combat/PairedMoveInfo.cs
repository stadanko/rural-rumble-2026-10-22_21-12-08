using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// A two-wrestler synchronized move starting (ARCHITECTURE 6), carried by
    /// <see cref="CombatEvents.PairedMoveStarted"/>. Lock-up slice: LockUp and BreakLockUp; Bodyslam and
    /// IrishWhip come in later steps. Both wrestlers' positions are already snapped when it is raised.
    /// </summary>
    public readonly struct PairedMoveInfo
    {
        /// <summary>Wrestler A: the wrestler doing the move (Lock Up state).</summary>
        public readonly WrestlerId Initiator;
        /// <summary>Wrestler B: the wrestler receiving the move (Locked Up state).</summary>
        public readonly WrestlerId Receiver;
        public readonly MoveId Move;
        /// <summary>The pair anchor on the arena plane: the point midway between the two snapped feet points.</summary>
        public readonly Vector2 Anchor;
        /// <summary>The initiator's left/right facing, +1 right / -1 left. The receiver faces the other way.</summary>
        public readonly int Facing;
        /// <summary>The move's timing, read from tuning at this moment (presentation scales its clips to it).</summary>
        public readonly MoveTiming Timing;

        public PairedMoveInfo(WrestlerId initiator, WrestlerId receiver, MoveId move, Vector2 anchor, int facing, MoveTiming timing)
        {
            Initiator = initiator;
            Receiver = receiver;
            Move = move;
            Anchor = anchor;
            Facing = facing;
            Timing = timing;
        }
    }
}
