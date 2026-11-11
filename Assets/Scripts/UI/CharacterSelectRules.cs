using System.Collections.Generic;
using RuralRumble.Core;

namespace RuralRumble.UI
{
    /// <summary>Pure decisions for the Character Select screen.</summary>
    public static class CharacterSelectRules
    {
        public const float NavigateThreshold = 0.5f;

        /// <summary>Returns the new highlighted index: x &lt;= -0.5 goes left, x &gt;= 0.5 goes right, no wrap.</summary>
        public static int Move(int index, float x, int count)
        {
            if (count <= 0) return 0;
            int next = index;
            if (x <= -NavigateThreshold) next = index - 1;
            else if (x >= NavigateThreshold) next = index + 1;
            if (next < 0) next = 0;
            if (next > count - 1) next = count - 1;
            return next;
        }

        /// <summary>Player gets the chosen wrestler; the CPU gets the other one (two-wrestler roster).</summary>
        public static MatchSetup BuildSetup(IReadOnlyList<WrestlerDefinition> wrestlers, int chosenIndex)
        {
            int cpuIndex = chosenIndex == 0 ? 1 : 0;
            if (cpuIndex >= wrestlers.Count) cpuIndex = chosenIndex;
            return new MatchSetup(wrestlers[chosenIndex], wrestlers[cpuIndex]);
        }
    }
}
