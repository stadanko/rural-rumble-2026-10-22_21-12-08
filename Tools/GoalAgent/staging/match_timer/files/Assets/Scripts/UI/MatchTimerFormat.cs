using System;

namespace RuralRumble.UI
{
    /// <summary>Formats elapsed match seconds as m:ss.</summary>
    public static class MatchTimerFormat
    {
        /// <summary>Whole seconds shown for an elapsed time: floored, negative or NaN becomes 0.</summary>
        public static int WholeSeconds(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || elapsedSeconds <= 0f) return 0;
            if (float.IsPositiveInfinity(elapsedSeconds) || elapsedSeconds >= int.MaxValue) return int.MaxValue;
            return (int)Math.Floor(elapsedSeconds);
        }

        /// <summary>m:ss text; minutes are not padded, seconds always have two digits.</summary>
        public static string Format(float elapsedSeconds)
        {
            return FormatWhole(WholeSeconds(elapsedSeconds));
        }

        private static string FormatWhole(int total)
        {
            int minutes = total / 60;
            int seconds = total % 60;
            return minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + seconds.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
