using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Pure red-flash opacity over time (D-046): full <c>peak</c> for <c>hold</c> seconds, then a linear fade to 0 over
    /// <c>fade</c> seconds. A new trigger restarts it, keeping the stronger peak and the longer hold if one is still
    /// running. Real time.
    /// </summary>
    public sealed class FlashEnvelope
    {
        private float peak;
        private float hold;
        private float fade;
        private float age = float.PositiveInfinity;

        public float Alpha
        {
            get
            {
                if (float.IsInfinity(age) || peak <= 0f)
                    return 0f;
                if (age <= hold)
                    return peak;
                if (fade <= 0f)
                    return 0f;
                float t = (age - hold) / fade;
                return t >= 1f ? 0f : peak * (1f - t);
            }
        }

        public bool IsActive => Alpha > 0f;

        public void Trigger(float peakAlpha, float holdSeconds, float fadeSeconds)
        {
            bool running = IsActive;
            float newPeak = Mathf.Clamp01(peakAlpha);
            float newHold = Mathf.Max(0f, holdSeconds);
            float newFade = Mathf.Max(0f, fadeSeconds);
            if (running)
            {
                newPeak = Mathf.Max(newPeak, Alpha);
                newHold = Mathf.Max(newHold, hold - age);
                newFade = Mathf.Max(newFade, fade);
            }
            peak = newPeak;
            hold = newHold;
            fade = newFade;
            age = 0f;
        }

        public void Advance(float realSeconds)
        {
            if (!float.IsInfinity(age) && realSeconds > 0f)
                age += realSeconds;
        }
    }
}
