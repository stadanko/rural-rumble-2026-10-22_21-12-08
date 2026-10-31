namespace RuralRumble.Core
{
    /// <summary>Who plays whom. Set by Character Select, read by MatchBootstrap (ARCHITECTURE 4.4).</summary>
    public readonly struct MatchSetup
    {
        public readonly WrestlerDefinition Player;
        public readonly WrestlerDefinition Cpu;

        public MatchSetup(WrestlerDefinition player, WrestlerDefinition cpu)
        {
            Player = player;
            Cpu = cpu;
        }
    }

    /// <summary>
    /// Outcome of a match (ARCHITECTURE 4.3). When <see cref="IsDraw"/> is true (double knockout on the
    /// same tick, D-030), Winner and Loser are meaningless.
    /// </summary>
    public readonly struct MatchResult
    {
        public readonly WrestlerId Winner;
        public readonly WrestlerId Loser;
        public readonly float ElapsedSeconds;
        /// <summary>Both wrestlers were knocked out on the same tick (D-030). Winner/Loser carry no meaning.</summary>
        public readonly bool IsDraw;

        public MatchResult(WrestlerId winner, WrestlerId loser, float elapsedSeconds, bool isDraw = false)
        {
            Winner = winner;
            Loser = loser;
            ElapsedSeconds = elapsedSeconds;
            IsDraw = isDraw;
        }

        /// <summary>A draw (D-030). Winner/Loser are set to Player/Cpu only as placeholders.</summary>
        public static MatchResult Draw(float elapsedSeconds) =>
            new MatchResult(WrestlerId.Player, WrestlerId.Cpu, elapsedSeconds, isDraw: true);
    }
}
