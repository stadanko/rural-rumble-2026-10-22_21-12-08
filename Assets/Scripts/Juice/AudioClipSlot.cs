using System;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>One sound: a clip and its playback volume. An empty clip is skipped silently.</summary>
    [Serializable]
    public sealed class AudioClipSlot
    {
        [Tooltip("Clip to play. Leave empty to stay silent (e.g. the Sound Wrangler hasn't delivered it yet).")]
        public AudioClip clip;

        [Tooltip("Playback volume, 0 to 1. Clips differ in loudness, so tune each one by ear.")]
        [Range(0f, 1f)] public float volume = 1f;

        public AudioClipSlot() { }

        public AudioClipSlot(float defaultVolume) => volume = defaultVolume;

        public bool IsPlayable => clip != null && volume > 0f;
    }
}
