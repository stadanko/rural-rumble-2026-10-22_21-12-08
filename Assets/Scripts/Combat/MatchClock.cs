using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Presentation-requested time control for the combat tick (D-046 hitstop and slow-motion knockout;
    /// ARCHITECTURE 4.2 "Match time hook"). Plain C#, owned by <see cref="CombatMatchRunner"/>; Juice calls the
    /// runner's request methods, never this class directly.
    ///
    /// It only turns a frame's real seconds into combat seconds. Rules never see it: <see cref="CombatMatch.Tick"/>
    /// receives fewer (or zero) seconds, so every combat timing stays the same in combat time.
    ///
    /// - Hitstop: combat time stops for the requested real seconds. Overlapping requests take the longer remaining
    ///   time (max), they never add up.
    /// - Slow motion: combat time runs at <see cref="SlowMotionScale"/> for the requested real seconds. A new request
    ///   replaces the current one (scale and remaining time).
    /// - Both timers count real time at the same time; while a hitstop is active the scale is 0, then the slow-motion
    ///   scale (if any time is left), then 1. A frame that crosses a boundary is split exactly.
    /// - Idle (no time left on either), <see cref="Advance"/> returns its argument unchanged, so the match ticks
    ///   exactly as it did before the hook existed.
    /// </summary>
    public sealed class MatchClock
    {
        /// <summary>Real seconds of hitstop left (0 = none).</summary>
        public float HitstopRemaining { get; private set; }
        /// <summary>Real seconds of slow motion left (0 = none).</summary>
        public float SlowMotionRemaining { get; private set; }
        /// <summary>Combat-time scale of the current slow motion (1 when there is none).</summary>
        public float SlowMotionScale { get; private set; } = 1f;

        /// <summary>True when neither a hitstop nor a slow motion is running.</summary>
        public bool IsIdle => HitstopRemaining <= 0f && SlowMotionRemaining <= 0f;

        /// <summary>The scale applied right now: 0 in a hitstop, the slow-motion scale, else 1.</summary>
        public float CurrentScale => HitstopRemaining > 0f ? 0f : SlowMotionRemaining > 0f ? SlowMotionScale : 1f;

        /// <summary>
        /// Freeze combat time for <paramref name="realSeconds"/>. If a hitstop is already running, the longer of the
        /// two remaining times is kept (no stacking). Zero, negative or NaN requests are ignored.
        /// </summary>
        public void RequestHitstop(float realSeconds)
        {
            if (!(realSeconds > 0f) || float.IsInfinity(realSeconds))
                return;
            HitstopRemaining = Mathf.Max(HitstopRemaining, realSeconds);
        }

        /// <summary>
        /// Run combat time at <paramref name="scale"/> (clamped to 0..1) for <paramref name="realSeconds"/>. Replaces any
        /// slow motion already running. Zero, negative or NaN durations, and a NaN scale, are ignored.
        /// </summary>
        public void RequestSlowMotion(float scale, float realSeconds)
        {
            if (!(realSeconds > 0f) || float.IsInfinity(realSeconds) || float.IsNaN(scale))
                return;
            SlowMotionScale = Mathf.Clamp01(scale);
            SlowMotionRemaining = realSeconds;
        }

        /// <summary>Ends any hitstop and slow motion now.</summary>
        public void Cancel()
        {
            HitstopRemaining = 0f;
            SlowMotionRemaining = 0f;
            SlowMotionScale = 1f;
        }

        /// <summary>
        /// Consumes <paramref name="realDt"/> real seconds and returns the combat seconds they are worth.
        /// Idle: returns <paramref name="realDt"/> unchanged.
        /// </summary>
        public float Advance(float realDt)
        {
            if (IsIdle || !(realDt > 0f))
                return realDt;

            // Within this frame [0, realDt]: hitstop covers [0, h], slow motion [0, s]; the rest runs at 1.
            float h = Mathf.Min(realDt, HitstopRemaining);
            float s = Mathf.Min(realDt, SlowMotionRemaining);
            float slowPart = Mathf.Max(0f, s - h);
            float normalPart = realDt - Mathf.Max(h, s);
            float combatDt = slowPart * SlowMotionScale + normalPart;

            HitstopRemaining = Mathf.Max(0f, HitstopRemaining - realDt);
            SlowMotionRemaining = Mathf.Max(0f, SlowMotionRemaining - realDt);
            if (SlowMotionRemaining <= 0f)
                SlowMotionScale = 1f;
            return combatDt;
        }
    }
}
