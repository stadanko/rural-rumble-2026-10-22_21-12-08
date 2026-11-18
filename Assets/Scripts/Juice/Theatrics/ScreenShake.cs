using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Pure screen-shake strength (D-046). One shake at a time: a new kick replaces the running one only when it is at
    /// least as strong as what is left of it, so a light hit never cuts a knockout shake short. Strength decays
    /// quadratically to 0 over the kick's duration. Real time: the caller advances it with unscaled seconds, so it keeps
    /// shaking through hitstop and slow motion.
    /// </summary>
    public sealed class ScreenShake
    {
        private float amplitude;
        private float duration;
        private float elapsed;

        /// <summary>Current strength in world units.</summary>
        public float Strength
        {
            get
            {
                if (duration <= 0f || elapsed >= duration)
                    return 0f;
                float k = 1f - elapsed / duration;
                return amplitude * k * k;
            }
        }

        public bool IsShaking => Strength > 0f;

        public void Kick(float newAmplitude, float seconds)
        {
            if (!(newAmplitude > 0f) || !(seconds > 0f))
                return;
            if (newAmplitude < Strength)
                return;
            amplitude = newAmplitude;
            duration = seconds;
            elapsed = 0f;
        }

        public void Advance(float realSeconds)
        {
            if (realSeconds > 0f)
                elapsed += realSeconds;
        }

        public void Stop()
        {
            amplitude = 0f;
            duration = 0f;
            elapsed = 0f;
        }

        /// <summary>Offset for a strength and a random direction (<paramref name="angle01"/>) and reach (<paramref name="reach01"/>), both 0..1.</summary>
        public static Vector2 Offset(float strength, float angle01, float reach01)
        {
            float a = angle01 * Mathf.PI * 2f;
            float r = strength * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(reach01));
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        /// <summary>Rounds to the art pixel grid (1 / <paramref name="pixelsPerUnit"/>) so the shake moves in whole pixels.</summary>
        public static Vector2 SnapToPixels(Vector2 offset, float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f)
                return offset;
            return new Vector2(Mathf.Round(offset.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(offset.y * pixelsPerUnit) / pixelsPerUnit);
        }
    }
}
