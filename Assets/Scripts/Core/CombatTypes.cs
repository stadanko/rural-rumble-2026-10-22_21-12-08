namespace RuralRumble.Core
{
    /// <summary>Which side of the match a wrestler is on.</summary>
    public enum WrestlerId
    {
        Player,
        Cpu
    }

    /// <summary>Exactly the twelve COMBAT_SPEC states. Do not add states without a COMBAT_SPEC change.</summary>
    public enum CombatState
    {
        Standing,
        Walking,
        Running,
        Striking,
        Struck,
        LockUp,
        LockedUp,
        Grappling,
        Grappled,
        KnockedDown,
        Recovering,
        KnockedOut
    }

    /// <summary>
    /// Moves named in ARCHITECTURE 4.2. Values are explicit because they are serialized (the Animator
    /// "Move" int parameter and MoveAnimationTable entries): never renumber or reorder, only append.
    /// </summary>
    public enum MoveId
    {
        None = 0,
        Strike1 = 1,
        Strike2 = 2,
        Strike3 = 3,
        /// <summary>Strike pressed while standing over a Knocked Down opponent (D-025).</summary>
        Stomp = 4,
        LockUp = 5,
        Bodyslam = 6,
        IrishWhip = 7,
        BreakLockUp = 8
    }

    /// <summary>Phase of the current move (ARCHITECTURE 4.2, attack timing).</summary>
    public enum MovePhase
    {
        None,
        Startup,
        Active,
        Recovery
    }
}
