using System;
using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>Menu moments that have a sound (ARCHITECTURE 4.6).</summary>
    public enum UiSound
    {
        Navigate,
        Confirm,
        Back,
        Start
    }

    /// <summary>
    /// UI screens raise these; the Juice Agent's menu audio player plays them (D-021: Juice plays audio).
    /// Lives in Core so UI never references Juice. A UI screen usually loads the next scene right after
    /// raising, so the player must survive scene loads for the sound to finish.
    /// </summary>
    public static class UiSoundEvents
    {
        public static event Action<UiSound> Raised;

        public static void Raise(UiSound sound) => Raised?.Invoke(sound);

        // Domain reload is disabled in Play Mode options; clear stale subscribers each Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Raised = null;
    }
}
