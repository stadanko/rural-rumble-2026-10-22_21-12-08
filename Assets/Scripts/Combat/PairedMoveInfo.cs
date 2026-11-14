using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// A two-wrestler synchronized move starting (ARCHITECTURE 6), carried by
    /// <see cref="CombatEvents.PairedMoveStarted"/>: LockUp, BreakLockUp, Bodyslam (step 2) and IrishWhip (step 3).
    /// Both wrestlers' positions and states are already set when it is raised.
    /// </summary>
    public readonly struct PairedMoveInfo
    {
        /// <summary>Wrestler A: the wrestler doing the move (Lock Up state; Grappling for a bodyslam or Irish Whip).</summary>
        public readonly WrestlerId Initiator;
        /// <summary>Wrestler B: the wrestler receiving the move (Locked Up state; Grappled for a bodyslam; Running for an Irish Whip).</summary>
        public readonly WrestlerId Receiver;
        public readonly MoveId Move;
        /// <summary>The pair anchor on the arena plane: the point midway between the two feet points when the move starts.</summary>
        public readonly Vector2 Anchor;
        /// <summary>The initiator's left/right facing, +1 right / -1 left. For a lock-up or bodyslam the receiver
        /// faces the other way. For an Irish Whip this is the whipper's lock-up facing, kept (locked) until the
        /// whip ends; the runner faces <see cref="WhipDirection"/>.</summary>
        public readonly int Facing;
        /// <summary>The move's timing, read from tuning at this moment (presentation scales its clips to it). For a
        /// bodyslam, the initiator's timing (D-033). For an Irish Whip, the whipper's choreography as a MoveTiming:
        /// Startup = f1 + f2 + f3 (press to release = start of f4), Active = 0, Recovery = f4 held (D-039).</summary>
        public readonly MoveTiming Timing;
        /// <summary>Irish Whip only (0 otherwise): the direction the runner is whipped and runs, +1 right / -1 left.</summary>
        public readonly int WhipDirection;
        /// <summary>Irish Whip only: true when the runner is whipped the way it faced in the lock-up, so it passes the
        /// whipper ("through", WhipDirection == -Facing); false when it is whipped the way the whipper faced
        /// ("away", WhipDirection == Facing).</summary>
        public readonly bool WhipThrough;

        public PairedMoveInfo(WrestlerId initiator, WrestlerId receiver, MoveId move, Vector2 anchor, int facing, MoveTiming timing,
            int whipDirection = 0, bool whipThrough = false)
        {
            Initiator = initiator;
            Receiver = receiver;
            Move = move;
            Anchor = anchor;
            Facing = facing;
            Timing = timing;
            WhipDirection = whipDirection;
            WhipThrough = whipThrough;
        }
    }
}
