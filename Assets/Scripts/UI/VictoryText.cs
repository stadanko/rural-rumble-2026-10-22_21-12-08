using RuralRumble.Core;

namespace RuralRumble.UI
{
    /// <summary>Which style the outcome line uses.</summary>
    public enum VictoryOutcomeKind
    {
        Win,
        Lose,
        Draw
    }

    /// <summary>Everything the Victory screen shows, already decided.</summary>
    public readonly struct VictoryContent
    {
        public readonly string Headline;
        public readonly string Outcome;
        public readonly VictoryOutcomeKind Kind;
        /// <summary>Quote including straight double quotes, or null when there is nothing to show.</summary>
        public readonly string Quote;
        /// <summary>Card shown first (the winner, or the player on a draw).</summary>
        public readonly WrestlerDefinition FirstCard;
        /// <summary>Second card, only set on a draw; null otherwise.</summary>
        public readonly WrestlerDefinition SecondCard;

        public VictoryContent(string headline, string outcome, VictoryOutcomeKind kind, string quote,
            WrestlerDefinition firstCard, WrestlerDefinition secondCard)
        {
            Headline = headline;
            Outcome = outcome;
            Kind = kind;
            Quote = quote;
            FirstCard = firstCard;
            SecondCard = secondCard;
        }
    }

    /// <summary>Builds the Victory screen text from a match result. No scene access.</summary>
    public static class VictoryText
    {
        public const float PreviewSeconds = 83f;

        /// <summary>True when no match was played: not a draw and Winner equals Loser (default result).</summary>
        public static bool IsNoMatch(MatchResult result) => !result.IsDraw && result.Winner == result.Loser;

        /// <summary>Returns a preview result when no match was played, otherwise the given result.</summary>
        public static MatchResult ResolveResult(MatchResult result) =>
            IsNoMatch(result) ? new MatchResult(WrestlerId.Player, WrestlerId.Cpu, PreviewSeconds) : result;

        /// <summary>Builds the screen content. lineRoll picks the quote: index = lineRoll modulo line count.</summary>
        public static VictoryContent Build(MatchResult result, WrestlerDefinition player, WrestlerDefinition cpu, int lineRoll)
        {
            result = ResolveResult(result);
            string timer = MatchTimerFormat.Format(result.ElapsedSeconds);

            if (result.IsDraw)
            {
                string draw = "Double Knockout! " + NameOf(player) + " and " + NameOf(cpu)
                    + " knocked each other out in " + timer;
                return new VictoryContent(draw, "Nobody Wins!", VictoryOutcomeKind.Draw, null, player, cpu);
            }

            WrestlerDefinition winner = result.Winner == WrestlerId.Player ? player : cpu;
            WrestlerDefinition loser = result.Winner == WrestlerId.Player ? cpu : player;
            string headline = "Victory! " + NameOf(winner) + " knocked out " + NameOf(loser) + " in " + timer;
            bool playerWon = result.Winner == WrestlerId.Player;
            return new VictoryContent(
                headline,
                playerWon ? "Congratulations!" : "Game Over!",
                playerWon ? VictoryOutcomeKind.Win : VictoryOutcomeKind.Lose,
                PickQuote(winner, lineRoll),
                winner,
                null);
        }

        static string NameOf(WrestlerDefinition def) => def != null && def.DisplayName != null ? def.DisplayName : "";

        static string PickQuote(WrestlerDefinition winner, int lineRoll)
        {
            if (winner == null) return null;
            var lines = winner.VictoryLines;
            if (lines == null || lines.Count == 0) return null;
            int index = lineRoll % lines.Count;
            if (index < 0) index += lines.Count;
            string line = lines[index];
            if (string.IsNullOrEmpty(line)) return null;
            return "\"" + line + "\"";
        }
    }
}
