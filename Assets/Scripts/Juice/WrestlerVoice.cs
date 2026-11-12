using System;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>One wrestler's voice clips for <see cref="CombatAudio"/>.</summary>
    [Serializable]
    public sealed class WrestlerVoice
    {
        [Tooltip("The wrestler these clips belong to (Assets/Data/Wrestlers/).")]
        public WrestlerDefinition wrestler;

        [Tooltip("Grunt when hit, slammed or crashing into the truck or pool. Empty = silent.")]
        public AudioClip hurt;

        [Tooltip("Groan when knocked out (match end). Empty = silent.")]
        public AudioClip knockout;

        [Tooltip("Volume for this wrestler's voice clips, 0 to 1.")]
        [Range(0f, 1f)] public float volume = 1f;
    }
}
