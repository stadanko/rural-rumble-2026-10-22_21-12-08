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

    /// <summary>Outcome of a match (ARCHITECTURE 4.3).</summary>
    public readonly struct MatchResult
    {
        public readonly WrestlerId Winner;
        public readonly WrestlerId Loser;
        public readonly float ElapsedSeconds;

        public MatchResult(WrestlerId winner, WrestlerId loser, float elapsedSeconds)
        {
            Winner = winner;
            Loser = loser;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
