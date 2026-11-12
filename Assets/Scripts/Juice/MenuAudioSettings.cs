using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Menu sound clips and volumes for <see cref="MenuAudioPlayer"/> (ARCHITECTURE 4.6). The player is created
    /// at runtime and can't hold Inspector references, so it loads this asset from
    /// <c>Assets/Prefabs/Effects/Resources/MenuAudioSettings.asset</c>. Edit volumes there.
    /// </summary>
    [CreateAssetMenu(fileName = "MenuAudioSettings", menuName = "Rural Rumble/Menu Audio Settings")]
    public sealed class MenuAudioSettings : ScriptableObject
    {
        /// <summary>Resources path (no extension) the player loads.</summary>
        public const string ResourcePath = "MenuAudioSettings";

        [Tooltip("Moving between choices (ui_navigate). The retro UI clips are louder than combat clips; ~0.35 matches them.")]
        [SerializeField] private AudioClipSlot navigate = new AudioClipSlot(0.35f);
        [Tooltip("Confirming a choice (ui_confirm).")]
        [SerializeField] private AudioClipSlot confirm = new AudioClipSlot(0.35f);
        [Tooltip("Going back (ui_back).")]
        [SerializeField] private AudioClipSlot back = new AudioClipSlot(0.35f);
        [Tooltip("Pressing Start on the Start screen (ui_start).")]
        [SerializeField] private AudioClipSlot start = new AudioClipSlot(0.35f);

        /// <summary>The slot for a menu sound, or null for an unknown value.</summary>
        public AudioClipSlot SlotFor(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.Navigate: return navigate;
                case UiSound.Confirm: return confirm;
                case UiSound.Back: return back;
                case UiSound.Start: return start;
                default: return null;
            }
        }
    }
}
