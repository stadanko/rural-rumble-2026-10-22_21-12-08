using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>The six D-049 footstep sets: one per surface and gait.</summary>
    public enum FootstepSetId
    {
        DirtWalk = 0,
        DirtRun = 1,
        GrassWalk = 2,
        GrassRun = 3,
        PuddleWalk = 4,
        PuddleRun = 5
    }

    /// <summary>
    /// Footstep decisions (D-049, ARCHITECTURE 4.8), plain C# so EditMode tests can cover them:
    /// which set a contact uses and how loud one step plays.
    /// </summary>
    public static class FootstepRules
    {
        public const int SetCount = 6;

        /// <summary>The set for a surface and gait. An unknown surface counts as Dirt (the yard default).</summary>
        public static FootstepSetId SetFor(SurfaceType surface, Gait gait)
        {
            bool run = gait == Gait.Run;
            switch (surface)
            {
                case SurfaceType.Grass: return run ? FootstepSetId.GrassRun : FootstepSetId.GrassWalk;
                case SurfaceType.Puddle: return run ? FootstepSetId.PuddleRun : FootstepSetId.PuddleWalk;
                default: return run ? FootstepSetId.DirtRun : FootstepSetId.DirtWalk;
            }
        }

        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        public static float LinearToDb(float linear) => linear > 0f ? 20f * Mathf.Log10(linear) : float.NegativeInfinity;

        /// <summary>Random level offset in dB: ± <paramref name="variationDb"/>, spread evenly by <paramref name="roll"/> (0..1).</summary>
        public static float JitterDb(float variationDb, float roll) =>
            Mathf.Max(0f, variationDb) * (Mathf.Clamp01(roll) * 2f - 1f);

        /// <summary>
        /// Unclamped linear volume of one step: master × 10^((set trim + clip gain + jitter) / 20).
        /// The clip gain brings that clip to the D-049 level (walk about −20, run about −17 dBFS loudest 50 ms);
        /// master below 1 is the headroom that lets positive clip gains play un-clipped.
        /// </summary>
        public static float RawVolume(float master, float setTrimDb, float clipGainDb, float jitterDb) =>
            Mathf.Max(0f, master) * DbToLinear(setTrimDb + clipGainDb + jitterDb);

        /// <summary><see cref="RawVolume"/> clamped to what an AudioSource can play (0..1).</summary>
        public static float Volume(float master, float setTrimDb, float clipGainDb, float jitterDb) =>
            Mathf.Clamp01(RawVolume(master, setTrimDb, clipGainDb, jitterDb));
    }
}
