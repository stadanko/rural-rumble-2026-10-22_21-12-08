using System;
using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Start screen intro channel (ARCHITECTURE 4.6). UI's Start screen raises <see cref="LogoLanded"/> once, when the
    /// falling "RURAL RUMBLE" logo slams down, or when a press skips the intro to the landed state. Juice subscribes
    /// (yard light reveal, shake, dust, thud). Lives in Core so UI never references Juice. Presentation only.
    /// Subscribers unsubscribe in OnDisable/OnDestroy; the event is also cleared at the start of every Play.
    /// </summary>
    public static class StartScreenEvents
    {
        /// <summary>The logo is down. Argument: true when a press skipped the intro (no thud, shake or dust; jump to full light).</summary>
        public static event Action<bool> LogoLanded;

        public static void RaiseLogoLanded(bool skipped) => LogoLanded?.Invoke(skipped);

        // Domain reload is disabled in Play Mode options; clear stale subscribers each Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => LogoLanded = null;
    }
}
