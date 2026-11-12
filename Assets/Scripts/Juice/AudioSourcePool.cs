using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// A few 2D AudioSources on one GameObject, so overlapping sounds don't cut each other off and each
    /// playback can have its own pitch. When every source is busy, the next one in turn (the oldest) is reused.
    /// </summary>
    public sealed class AudioSourcePool
    {
        private readonly AudioSource[] sources;
        private int next;

        public AudioSourcePool(GameObject host, int count)
        {
            sources = new AudioSource[Mathf.Max(1, count)];
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                sources[i] = s;
            }
        }

        /// <summary>Plays the clip; does nothing for a missing clip or zero volume.</summary>
        public void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || volume <= 0f)
                return;

            int index = next;
            for (int i = 0; i < sources.Length; i++)
            {
                int candidate = (next + i) % sources.Length;
                if (sources[candidate] != null && !sources[candidate].isPlaying)
                {
                    index = candidate;
                    break;
                }
            }
            next = (index + 1) % sources.Length;

            AudioSource source = sources[index];
            if (source == null)
                return;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.Play();
        }

        public void Play(AudioClipSlot slot, float pitch)
        {
            if (slot != null && slot.IsPlayable)
                Play(slot.clip, slot.volume, pitch);
        }
    }
}
