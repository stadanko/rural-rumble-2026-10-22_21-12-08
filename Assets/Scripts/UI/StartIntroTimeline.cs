using System;
using UnityEngine;

namespace RuralRumble.UI
{
    /// <summary>Where the Start screen intro is at a given time since the screen opened.</summary>
    public enum StartIntroPhase
    {
        /// <summary>Logo above the screen, yard dim.</summary>
        Waiting,
        /// <summary>Logo falling (ease-in).</summary>
        Falling,
        /// <summary>Logo landed: squash-and-recover bounce.</summary>
        Bouncing,
        /// <summary>Logo at rest, lights coming up, prompt not shown yet.</summary>
        Revealing,
        /// <summary>Prompt shown; the next press starts the game.</summary>
        Ready
    }

    /// <summary>Logo transform at one moment. Offset is a fraction (0..1) of the off-screen distance above rest.</summary>
    public readonly struct StartLogoPose
    {
        public readonly float OffsetFraction;
        public readonly Vector2 Scale;
        /// <summary>Impact jolt in panel pixels (x right, y down).</summary>
        public readonly Vector2 Shake;

        public StartLogoPose(float offsetFraction, Vector2 scale, Vector2 shake)
        {
            OffsetFraction = offsetFraction;
            Scale = scale;
            Shake = shake;
        }

        public static StartLogoPose Rest => new StartLogoPose(0f, Vector2.one, Vector2.zero);
    }

    /// <summary>
    /// Start screen logo-drop timing (Start screen upgrade, ARCHITECTURE 4.6 "Start screen intro"). All values are
    /// Starting points, tuned in the Inspector on <see cref="StartScreenController"/>. Pure: time comes from the caller.
    /// </summary>
    [Serializable]
    public sealed class StartIntroTimeline
    {
        [Tooltip("Seconds after the screen opens before the logo starts to fall.")]
        [Min(0f)] public float startDelay = 0.3f;
        [Tooltip("Seconds the fall from above the screen takes (ease-in, accelerating).")]
        [Min(0f)] public float fallDuration = 0.45f;
        [Tooltip("Ease-in power for the fall: 2 = gravity-like, higher = later, harder slam.")]
        [Min(1f)] public float fallEasePower = 2f;
        [Tooltip("Seconds of squash-and-recover bounce after impact.")]
        [Min(0f)] public float bounceDuration = 0.25f;
        [Tooltip("Peak squash: 0.18 = 18% wider and 18% shorter on impact.")]
        [Range(0f, 0.5f)] public float squashAmount = 0.18f;
        [Tooltip("Seconds the logo jolts after impact.")]
        [Min(0f)] public float shakeDuration = 0.2f;
        [Tooltip("Peak jolt in panel pixels (1920x1080 reference).")]
        [Min(0f)] public float shakeAmplitude = 10f;
        [Tooltip("Seconds after impact before \"Press Any Button to Start\" appears.")]
        [Min(0f)] public float promptDelay = 0.6f;

        /// <summary>Seconds since the screen opened at which the logo lands.</summary>
        public float ImpactTime => Mathf.Max(0f, startDelay) + Mathf.Max(0f, fallDuration);

        /// <summary>Seconds since the screen opened at which the prompt appears.</summary>
        public float PromptTime => ImpactTime + Mathf.Max(0f, promptDelay);

        public StartIntroPhase PhaseAt(float t)
        {
            if (t >= PromptTime) return StartIntroPhase.Ready;
            if (t >= ImpactTime)
                return t < ImpactTime + Mathf.Max(0f, bounceDuration) ? StartIntroPhase.Bouncing : StartIntroPhase.Revealing;
            return t < Mathf.Max(0f, startDelay) ? StartIntroPhase.Waiting : StartIntroPhase.Falling;
        }

        /// <summary>0 = above the screen, 1 = at rest. Eased-in (accelerating) between start of fall and impact.</summary>
        public float FallProgress(float t)
        {
            float start = Mathf.Max(0f, startDelay);
            if (t <= start) return t >= ImpactTime ? 1f : 0f;
            if (t >= ImpactTime) return 1f;
            float u = (t - start) / Mathf.Max(0f, fallDuration);
            return Mathf.Pow(Mathf.Clamp01(u), Mathf.Max(1f, fallEasePower));
        }

        /// <summary>
        /// Signed squash at time t: positive = wider and shorter, negative = taller and narrower (overshoot).
        /// A decaying cosine: full squash at impact, overshoot at 40%, small squash at 80%, zero at the end.
        /// </summary>
        public float SquashAt(float t)
        {
            float d = Mathf.Max(0f, bounceDuration);
            float since = t - ImpactTime;
            if (since < 0f || since >= d || d <= 0f) return 0f;
            float u = since / d;
            return squashAmount * (1f - u) * Mathf.Cos(u * 2.5f * Mathf.PI);
        }

        /// <summary>Impact jolt in panel pixels, decaying to zero over the shake duration.</summary>
        public Vector2 ShakeAt(float t)
        {
            float d = Mathf.Max(0f, shakeDuration);
            float since = t - ImpactTime;
            if (since < 0f || since >= d || d <= 0f) return Vector2.zero;
            float u = since / d;
            float amp = shakeAmplitude * (1f - u);
            // Fixed (not random) wobble so the jolt is the same every time and testable.
            return new Vector2(amp * Mathf.Sin(u * 7f * Mathf.PI), amp * 0.6f * Mathf.Cos(u * 9f * Mathf.PI));
        }

        public StartLogoPose PoseAt(float t)
        {
            float squash = SquashAt(t);
            return new StartLogoPose(1f - FallProgress(t), new Vector2(1f + squash, 1f - squash), ShakeAt(t));
        }
    }
}
