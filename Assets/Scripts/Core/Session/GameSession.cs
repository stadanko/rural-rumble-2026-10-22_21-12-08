using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Carries the selected wrestlers and the last result across single scene loads (ARCHITECTURE 4.4).
    /// Reset on every Play Mode start, so it also works with domain reload disabled.
    /// </summary>
    public static class GameSession
    {
        private static MatchSetup setup;

        public static MatchSetup Setup
        {
            get => setup;
            set
            {
                setup = value;
                HasSetup = true;
            }
        }

        /// <summary>False when Match is opened directly in the Editor; MatchBootstrap then uses roster entries 0 and 1.</summary>
        public static bool HasSetup { get; private set; }

        public static MatchResult LastResult { get; set; }

        public static void Reset()
        {
            setup = default;
            HasSetup = false;
            LastResult = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Reset();
    }
}
