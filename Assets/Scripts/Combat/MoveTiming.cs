using System;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Timing of one move, in seconds (ARCHITECTURE 4.2, Attack timing). Startup = wind-up to the contact
    /// frame, Active = hit window (contact is checked only here), Recovery = after the hit window.
    /// Serializable so <see cref="CombatTuning"/> can hold one per move and the designer can edit it in
    /// the Inspector (D-005). Combat is the authority; presentation only matches clip speed to it.
    /// </summary>
    [Serializable]
    public struct MoveTiming
    {
        [Tooltip("Wind-up: seconds from move start to the contact frame.")]
        [Min(0f)] public float Startup;
        [Tooltip("Hit window: seconds during which contact can land.")]
        [Min(0f)] public float Active;
        [Tooltip("Seconds after the hit window before the wrestler can act again.")]
        [Min(0f)] public float Recovery;

        public MoveTiming(float startup, float active, float recovery)
        {
            Startup = startup;
            Active = active;
            Recovery = recovery;
        }

        /// <summary>Startup + Active + Recovery.</summary>
        public float Total => Startup + Active + Recovery;
    }
}
