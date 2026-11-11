using UnityEngine;

namespace RuralRumble.UI
{
    /// <summary>
    /// Damage-trail math for one health bar. Values are bar fractions 0..1. On a hit the fill drops at once,
    /// the trail stays at the old value for the hold time, then drains linearly to the fill over the drain time.
    /// Pure: no Unity objects, time comes from the caller.
    /// </summary>
    public sealed class HealthTrail
    {
        private float holdRemaining;
        private float drainRate = -1f; // fraction per second; negative = not computed yet

        /// <summary>Current health fraction (the colored fill).</summary>
        public float Fill { get; private set; }

        /// <summary>White trail fraction. Always at or above <see cref="Fill"/>.</summary>
        public float Trail { get; private set; }

        /// <summary>Health as a 0..1 fraction. A max of zero or less gives 0.</summary>
        public static float ToFraction(float health, float maxHealth) =>
            maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth);

        /// <summary>Sets fill and trail to the same value with no trail shown.</summary>
        public void Reset(float fraction)
        {
            Fill = Trail = Mathf.Clamp01(fraction);
            holdRemaining = 0f;
            drainRate = -1f;
        }

        /// <summary>New health fraction. A drop restarts the hold; a rise pulls the trail up with the fill.</summary>
        public void SetFill(float fraction, float holdSeconds)
        {
            fraction = Mathf.Clamp01(fraction);
            bool dropped = fraction < Fill;
            Fill = fraction;
            if (Trail < Fill)
                Trail = Fill;
            if (dropped)
            {
                holdRemaining = Mathf.Max(0f, holdSeconds);
                drainRate = -1f;
            }
        }

        /// <summary>Advances the trail by dt seconds (pass unscaled time).</summary>
        public void Tick(float dt, float drainSeconds)
        {
            if (Trail <= Fill)
            {
                Trail = Fill;
                return;
            }

            float remaining = Mathf.Max(0f, dt);
            if (holdRemaining > 0f)
            {
                float used = Mathf.Min(holdRemaining, remaining);
                holdRemaining -= used;
                remaining -= used;
                if (holdRemaining > 0f)
                    return;
            }

            if (drainRate < 0f)
                drainRate = drainSeconds <= 0f ? float.PositiveInfinity : (Trail - Fill) / drainSeconds;

            Trail = float.IsInfinity(drainRate) ? Fill : Mathf.Max(Fill, Trail - drainRate * remaining);
        }
    }
}
