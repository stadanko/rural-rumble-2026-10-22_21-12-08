using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>One footstep variant: a clip and the gain that brings it to its set's D-049 level.</summary>
    [Serializable]
    public sealed class FootstepClip
    {
        [Tooltip("The step clip. Empty (or missing store audio) is skipped silently.")]
        public AudioClip clip;

        [Tooltip("Gain in dB that brings this clip to the D-049 level (walk about -20, run about -17 dBFS loudest 50 ms). " +
                 "From the clip's ASSET_REGISTRY row ('this clip alone needs'). Applied under the master volume, so positive values are fine.")]
        [Range(-24f, 12f)] public float gainDb;

        public FootstepClip() { }

        public FootstepClip(AudioClip clip, float gainDb)
        {
            this.clip = clip;
            this.gainDb = gainDb;
        }
    }

    /// <summary>The variants of one surface and gait, plus a trim the designer uses to rebalance the set.</summary>
    [Serializable]
    public sealed class FootstepSet
    {
        [Tooltip("Rebalance this whole set in dB (Starting point 0). Added to every clip's gain.")]
        [Range(-24f, 12f)] public float trimDb;

        [Tooltip("Variants, played at random with no immediate repeat (two variants alternate).")]
        public FootstepClip[] clips = Array.Empty<FootstepClip>();

        public bool IsPlayable(int index) =>
            clips != null && index >= 0 && index < clips.Length && clips[index] != null && clips[index].clip != null;
    }

    /// <summary>
    /// Footstep clips and levels for <see cref="FootstepAudio"/> (D-049, ARCHITECTURE 4.8). One asset,
    /// <c>Assets/Prefabs/Effects/FootstepSettings.asset</c>, already referenced by the FootstepAudio prefab.
    /// The clips are git-ignored store audio, so a clone without the pack sees empty slots and plays nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "FootstepSettings", menuName = "Rural Rumble/Footstep Settings")]
    public sealed class FootstepSettings : ScriptableObject
    {
        [Header("Level")]
        [Tooltip("Master footstep volume, 0 to 1 (Starting point 0.6, about -4.4 dB). At 1 a step plays at its D-049 level " +
                 "(walk about -20, run about -17 dBFS); the default sits 4.4 dB under that so the loudest clip gain (+3.3 dB) " +
                 "plus the level variation (+1 dB) still fits under an AudioSource's maximum of 1. Higher than about 0.6 " +
                 "and the loudest run variants are capped (play quieter than intended).")]
        [Range(0f, 1f)] public float masterVolume = 0.6f;

        [Tooltip("Random level variation per step in dB: each step plays at its level ± this (Starting point 1, as in the designer's demos).")]
        [Range(0f, 6f)] public float levelVariationDb = 1f;

        [Tooltip("Random pitch spread per step: pitch is 1 ± this (Starting point 0, off, as in the designer's demos). " +
                 "Up to about 0.03 keeps the character of the clips.")]
        [Range(0f, 0.1f)] public float pitchVariation = 0f;

        [Header("Sets (D-049)")]
        [Tooltip("Dirt walking: the yard default (gravel clips).")]
        public FootstepSet dirtWalk = new FootstepSet();
        [Tooltip("Dirt running (gravel clips).")]
        public FootstepSet dirtRun = new FootstepSet();
        [Tooltip("Grass walking.")]
        public FootstepSet grassWalk = new FootstepSet();
        [Tooltip("Grass running.")]
        public FootstepSet grassRun = new FootstepSet();
        [Tooltip("Puddle walking (mud clips).")]
        public FootstepSet puddleWalk = new FootstepSet();
        [Tooltip("Puddle running (mud clips).")]
        public FootstepSet puddleRun = new FootstepSet();

        public FootstepSet SetFor(FootstepSetId id)
        {
            switch (id)
            {
                case FootstepSetId.DirtWalk: return dirtWalk;
                case FootstepSetId.DirtRun: return dirtRun;
                case FootstepSetId.GrassWalk: return grassWalk;
                case FootstepSetId.GrassRun: return grassRun;
                case FootstepSetId.PuddleWalk: return puddleWalk;
                case FootstepSetId.PuddleRun: return puddleRun;
                default: return null;
            }
        }

        public FootstepSet SetFor(SurfaceType surface, Gait gait) => SetFor(FootstepRules.SetFor(surface, gait));

        /// <summary>
        /// The loudest step any variant can ask for (gain plus the full level variation), before the 0..1 cap.
        /// Above 1 means some steps get capped; lower the master or that clip's gain.
        /// </summary>
        public float MaxRawVolume()
        {
            float max = 0f;
            for (int s = 0; s < FootstepRules.SetCount; s++)
            {
                FootstepSet set = SetFor((FootstepSetId)s);
                if (set?.clips == null)
                    continue;
                foreach (FootstepClip c in set.clips)
                {
                    if (c == null)
                        continue;
                    max = Mathf.Max(max, FootstepRules.RawVolume(masterVolume, set.trimDb, c.gainDb, levelVariationDb));
                }
            }
            return max;
        }
    }
}
