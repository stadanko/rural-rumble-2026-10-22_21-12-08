using System;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Picks a random variant of one footstep set, never the same one twice in a row (D-049). Only playable
    /// variants (clip present) count; with two playable variants that is strict alternation, with one it
    /// repeats, with none it returns -1. Plain C#.
    /// </summary>
    public sealed class VariantPicker
    {
        /// <summary>Index of the last variant picked, or -1.</summary>
        public int Last { get; private set; } = -1;

        /// <param name="count">Number of variants in the set.</param>
        /// <param name="isPlayable">Whether variant i has a clip. Null means all are playable.</param>
        /// <param name="roll">Random value 0..1 (1 is treated as just under 1).</param>
        public int Pick(int count, Func<int, bool> isPlayable, float roll)
        {
            int playable = 0;
            bool lastPlayable = false;
            for (int i = 0; i < count; i++)
            {
                if (isPlayable != null && !isPlayable(i))
                    continue;
                playable++;
                if (i == Last)
                    lastPlayable = true;
            }
            if (playable == 0)
                return -1;

            int choices = playable;
            if (lastPlayable && playable > 1)
                choices--; // skip the previous variant

            int k = Math.Min(choices - 1, (int)(Math.Max(0f, roll) * choices));
            for (int i = 0; i < count; i++)
            {
                if (isPlayable != null && !isPlayable(i))
                    continue;
                if (i == Last && lastPlayable && playable > 1)
                    continue;
                if (k == 0)
                {
                    Last = i;
                    return i;
                }
                k--;
            }
            return -1; // not reached
        }

        public void Reset() => Last = -1;
    }
}
