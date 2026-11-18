using UnityEngine;

namespace RuralRumble.Presentation
{
    /// <summary>
    /// Receiver for the wrestler clips' AnimationEvents (D-049, ARCHITECTURE 4.8). Sits on the child "Sprite" next to the
    /// Animator (Unity only delivers AnimationEvents to components on the Animator's GameObject) and forwards each event
    /// to the root's <see cref="WrestlerView"/>, which decides whether it is a foot contact to raise. Never decides
    /// anything itself and never touches combat. Added to the prefab by WrestlerAnimationBuilder; WrestlerView also adds
    /// it at runtime if a prefab lacks it, so an event never goes without a receiver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WrestlerAnimationEventRelay : MonoBehaviour
    {
        /// <summary>AnimationEvent function name on the walk / run foot-contact frames (intParameter 0 left, 1 right).</summary>
        public const string FootContactFunction = nameof(OnFootContact);

        private WrestlerView view;

        /// <summary>Called by the Animator for each foot-contact AnimationEvent of the walk and run clips.</summary>
        public void OnFootContact(AnimationEvent animationEvent)
        {
            if (view == null)
                view = GetComponentInParent<WrestlerView>();
            if (view != null)
                view.HandleFootContactEvent(animationEvent);
        }
    }
}
