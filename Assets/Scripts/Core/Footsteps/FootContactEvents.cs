using System;
using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>Which gait clip planted the foot (D-049). Read from presentation's Animator state, never from timers.</summary>
    public enum Gait
    {
        Walk = 0,
        Run = 1
    }

    /// <summary>Which foot met the ground (D-049). Marked per contact frame by Unity Animation &amp; Tooling.</summary>
    public enum Foot
    {
        Left = 0,
        Right = 1
    }

    /// <summary>One foot of one wrestler touching the ground (D-049, ARCHITECTURE 4.8). Presentation data only.</summary>
    public readonly struct FootContact
    {
        public readonly WrestlerId Wrestler;
        public readonly Foot Foot;
        public readonly Gait Gait;
        /// <summary>World position of the planted foot on the arena plane (same frame as IWrestlerView.Position).</summary>
        public readonly Vector2 Position;

        public FootContact(WrestlerId wrestler, Foot foot, Gait gait, Vector2 position)
        {
            Wrestler = wrestler;
            Foot = foot;
            Gait = gait;
            Position = position;
        }

        public override string ToString() => $"FootContact({Wrestler}, {Foot}, {Gait}, {Position})";
    }

    /// <summary>
    /// Foot-contact channel (D-049, ARCHITECTURE 4.8). Presentation (WrestlerView) raises one contact per foot plant in
    /// the Walking and Running Animator states; Juice subscribes and plays the footstep. Lives in Core, like
    /// <see cref="UiSoundEvents"/>, so Juice never references Presentation. Raising has no combat effect.
    /// Subscribers unsubscribe in OnDisable/OnDestroy; the event is also cleared at the start of every Play.
    /// </summary>
    public static class FootContactEvents
    {
        public static event Action<FootContact> Contact;

        public static void Raise(FootContact contact) => Contact?.Invoke(contact);

        // Domain reload is disabled in Play Mode options; clear stale subscribers each Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Contact = null;
    }
}
