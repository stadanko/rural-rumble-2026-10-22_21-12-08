using System;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RuralRumble.Juice
{
    /// <summary>What one footstep played (for logs and tests).</summary>
    public readonly struct FootstepPlayback
    {
        public readonly FootContact Contact;
        public readonly SurfaceType Surface;
        public readonly FootstepSetId Set;
        public readonly int Variant;
        public readonly AudioClip Clip;
        public readonly float Volume;
        public readonly float Pitch;

        public FootstepPlayback(FootContact contact, SurfaceType surface, FootstepSetId set, int variant, AudioClip clip, float volume, float pitch)
        {
            Contact = contact;
            Surface = surface;
            Set = set;
            Variant = variant;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
        }
    }

    /// <summary>
    /// Footstep player (D-049, ARCHITECTURE 4.8). Listens to <see cref="FootContactEvents.Contact"/>, reads the
    /// surface under the planted foot from <c>MatchBootstrap.Current.SurfaceMap</c> (no map = Dirt), picks the
    /// set for that surface and gait, a variant with no immediate repeat, and plays it at once on its own pooled
    /// 2D AudioSources, so the sound starts on the contact frame and overlapping steps don't cut each other off.
    /// Presentation only: reads the match, never changes it. Plays only while a match is running (not over, not
    /// in hitstop). Missing clips are skipped silently. Decisions live in <see cref="FootstepRules"/> and
    /// <see cref="VariantPicker"/>; clips and levels in <see cref="FootstepSettings"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [Tooltip("Clips, gains and level settings (Assets/Prefabs/Effects/FootstepSettings.asset). Empty = silent.")]
        [SerializeField] private FootstepSettings settings;

        [Tooltip("How many steps can sound at once (both wrestlers, overlapping tails). Read on enable. Starting point 6.")]
        [Range(1, 16)] [SerializeField] private int simultaneousSteps = 6;

        private AudioSourcePool pool;
        private readonly VariantPicker[] pickers = new VariantPicker[FootstepRules.SetCount];

        /// <summary>Raised after a step starts playing. Diagnostics only.</summary>
        public event Action<FootstepPlayback> Played;

        public FootstepSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        private void Awake()
        {
            for (int i = 0; i < pickers.Length; i++)
                pickers[i] = new VariantPicker();
        }

        private void OnEnable()
        {
            if (pool == null)
                pool = new AudioSourcePool(gameObject, simultaneousSteps);
            FootContactEvents.Contact -= OnContact;
            FootContactEvents.Contact += OnContact;
        }

        private void OnDisable() => FootContactEvents.Contact -= OnContact;

        private void OnDestroy() => FootContactEvents.Contact -= OnContact;

        private void OnContact(FootContact contact)
        {
            if (settings == null)
                return;

            MatchBootstrap bootstrap = MatchBootstrap.Current;
            if (bootstrap == null || bootstrap.Match == null || bootstrap.Match.IsOver)
                return;
            // Belt and braces: presentation raises nothing while paused, but never step during hitstop.
            if (bootstrap.Runner != null && bootstrap.Runner.CombatTimeScale <= 0f)
                return;

            ArenaSurfaceMap map = bootstrap.SurfaceMap;
            SurfaceType surface = map != null ? map.SurfaceAt(contact.Position) : SurfaceType.Dirt;
            FootstepSetId setId = FootstepRules.SetFor(surface, contact.Gait);
            FootstepSet set = settings.SetFor(setId);
            if (set == null || set.clips == null)
                return;

            int variant = pickers[(int)setId].Pick(set.clips.Length, set.IsPlayable, Random.value);
            if (variant < 0)
                return;

            FootstepClip step = set.clips[variant];
            float jitter = FootstepRules.JitterDb(settings.levelVariationDb, Random.value);
            float volume = FootstepRules.Volume(settings.masterVolume, set.trimDb, step.gainDb, jitter);
            float pitch = CombatSoundRules.Pitch(settings.pitchVariation, Random.value);
            pool.Play(step.clip, volume, pitch);

            Played?.Invoke(new FootstepPlayback(contact, surface, setId, variant, step.clip, volume, pitch));
        }
    }
}
