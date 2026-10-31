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

    /// <summary>Moves named in ARCHITECTURE 4.2.</summary>
    public enum MoveId
    {
        None,
        Strike1,
        Strike2,
        Strike3,
        LockUp,
        Bodyslam,
        IrishWhip,
        BreakLockUp
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
