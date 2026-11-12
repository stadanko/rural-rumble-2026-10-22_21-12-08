using RuralRumble.Core;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Keeps a bodyslam from thudding twice (ARCHITECTURE 4.6). The body thud plays at the bodyslam impact;
    /// the Knocked Down that follows for the same wrestler (about 1.2 s later) must stay quiet, and so must a
    /// knockout fall on the impact itself. Times are match seconds. Plain C# for EditMode tests.
    /// </summary>
    public sealed class BodyslamThudFilter
    {
        /// <summary>A knockout within this many seconds of the impact counts as the same fall.</summary>
        public const float SameMomentSeconds = 0.05f;

        private readonly float[] impactTime = { float.NegativeInfinity, float.NegativeInfinity };

        /// <summary>How long after a bodyslam impact that wrestler's Knocked Down stays quiet.</summary>
        public float WindowSeconds { get; set; }

        public BodyslamThudFilter(float windowSeconds) => WindowSeconds = windowSeconds;

        /// <summary>A bodyslam landed on <paramref name="defender"/> (its thud has played).</summary>
        public void NoteBodyslamImpact(WrestlerId defender, float now) => impactTime[(int)defender] = now;

        /// <summary>
        /// Whether this Knocked Down plays a thud. False (and the impact is used up) when it follows a
        /// bodyslam impact on the same wrestler within the window.
        /// </summary>
        public bool ShouldThudOnKnockdown(WrestlerId wrestler, float now)
        {
            float since = now - impactTime[(int)wrestler];
            if (since >= 0f && since <= WindowSeconds)
            {
                impactTime[(int)wrestler] = float.NegativeInfinity;
                return false;
            }
            return true;
        }

        /// <summary>Whether a knockout fall plays a thud: not when the bodyslam impact itself knocked out.</summary>
        public bool ShouldThudOnKnockout(WrestlerId wrestler, float now)
        {
            float since = now - impactTime[(int)wrestler];
            return !(since >= 0f && since <= SameMomentSeconds);
        }
    }
}
