using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Pure state of the Start screen yard light reveal (ARCHITECTURE 4.6 "Start screen intro"). The yard starts dim; the
    /// logo landing ramps it to full with an ease-out; a skip snaps it to full. Real time: the caller advances it with
    /// unscaled seconds. Presentation only.
    /// </summary>
    public sealed class StartReveal
    {
        public enum Phase
        {
            Dim,
            Ramping,
            Full,
        }

        private readonly float dimLevel;
        private readonly float rampSeconds;
        private readonly float failsafeSeconds;
        private float elapsed;

        /// <param name="dimLevel">Brightness multiplier before the logo lands (0..1).</param>
        /// <param name="rampSeconds">Seconds from dim to full after a landing. 0 or less snaps.</param>
        /// <param name="failsafeSeconds">If no landing arrives within this many seconds, ramp up anyway (no slam). 0 or less = never.</param>
        public StartReveal(float dimLevel, float rampSeconds, float failsafeSeconds)
        {
            this.dimLevel = Mathf.Clamp01(dimLevel);
            this.rampSeconds = rampSeconds;
            this.failsafeSeconds = failsafeSeconds;
        }

        public Phase Current { get; private set; } = Phase.Dim;

        /// <summary>Light multiplier now (dim level .. 1).</summary>
        public float Brightness
        {
            get
            {
                switch (Current)
                {
                    case Phase.Dim: return dimLevel;
                    case Phase.Ramping: return StartIntroRules.BrightnessAt(elapsed, rampSeconds, dimLevel);
                    default: return 1f;
                }
            }
        }

        /// <summary>
        /// The logo is down. Returns true when this landing should play the slam (shake and dust): only the first,
        /// unskipped landing while the yard is still dim. A skip always goes straight to full light.
        /// </summary>
        public bool Land(bool skipped)
        {
            if (skipped)
            {
                Current = Phase.Full;
                return false;
            }
            if (Current != Phase.Dim)
                return false;
            StartRamp();
            return true;
        }

        public void Advance(float realSeconds)
        {
            if (!(realSeconds > 0f))
                return;
            elapsed += realSeconds;
            if (Current == Phase.Dim)
            {
                if (failsafeSeconds > 0f && elapsed >= failsafeSeconds)
                    StartRamp();
            }
            else if (Current == Phase.Ramping && elapsed >= rampSeconds)
            {
                Current = Phase.Full;
            }
        }

        private void StartRamp()
        {
            elapsed = 0f;
            Current = rampSeconds > 0f ? Phase.Ramping : Phase.Full;
        }
    }

    /// <summary>Pure helpers for the Start screen intro juice: the light ramp, dust launch and UI panel to screen mapping.</summary>
    public static class StartIntroRules
    {
        /// <summary>Cubic ease-out, 0..1 to 0..1 (fast start, gentle finish).</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Brightness <paramref name="elapsed"/> seconds into a ramp from <paramref name="dimLevel"/> to 1.</summary>
        public static float BrightnessAt(float elapsed, float rampSeconds, float dimLevel)
        {
            if (rampSeconds <= 0f)
                return 1f;
            return Mathf.Lerp(Mathf.Clamp01(dimLevel), 1f, EaseOut(elapsed / rampSeconds));
        }

        /// <summary>
        /// Where along the logo's bottom edge a dust puff starts, as a signed fraction of the half width (-1 left end, +1
        /// right end). <paramref name="side01"/> picks the side (below 0.5 = left), <paramref name="along01"/> the place
        /// between <paramref name="innerStart"/> and the end, so most puffs start toward the ends and fly out past them.
        /// </summary>
        public static float SpawnFraction(float side01, float along01, float innerStart)
        {
            float side = side01 < 0.5f ? -1f : 1f;
            return side * Mathf.Lerp(Mathf.Clamp01(innerStart), 1f, Mathf.Clamp01(along01));
        }

        /// <summary>
        /// Launch velocity for a puff that starts at <paramref name="fraction"/> (see <see cref="SpawnFraction"/>): sideways,
        /// away from the logo's centre, faster toward the ends (50% to 100% of <paramref name="speed"/>, times 0.8 to 1.2 from
        /// <paramref name="speed01"/>), and a small lift (20% to 100% of <paramref name="rise"/> from <paramref name="rise01"/>).
        /// </summary>
        public static Vector2 DustVelocity(float fraction, float speed, float rise, float speed01, float rise01)
        {
            float side = fraction < 0f ? -1f : 1f;
            float s = speed * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Mathf.Abs(fraction))) * Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(speed01));
            return new Vector2(side * s, rise * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(rise01)));
        }

        /// <summary>
        /// A UI Toolkit panel point (origin top-left, y down, panel units) to a screen point (origin bottom-left, y up,
        /// pixels), for a panel of <paramref name="panelSize"/> stretched over a screen of <paramref name="screenSize"/>.
        /// </summary>
        public static Vector2 PanelToScreen(Vector2 panelPoint, Vector2 panelSize, Vector2 screenSize)
        {
            float sx = panelSize.x > 0f ? screenSize.x / panelSize.x : 1f;
            float sy = panelSize.y > 0f ? screenSize.y / panelSize.y : 1f;
            return new Vector2(panelPoint.x * sx, screenSize.y - panelPoint.y * sy);
        }
    }
}
