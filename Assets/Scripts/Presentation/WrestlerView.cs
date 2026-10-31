using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Presentation
{
    /// <summary>
    /// Presentation for one wrestler (ARCHITECTURE 4.2, D-021). Sits on the wrestler prefab root and is
    /// bound at runtime to that wrestler's read-only <see cref="IWrestlerView"/>. Every frame it copies the
    /// view's current state and facing into the child Animator and flips the child SpriteRenderer.
    ///
    /// Listens, never decides: it never changes combat state, and never writes the root transform
    /// (CombatMatchRunner owns the root position).
    ///
    /// It reads the view's state each frame rather than replaying StateChanged events, so a transition
    /// that goes out and back inside one combat tick (e.g. Standing -> Running -> Standing when a run
    /// starts against a wall) never reaches the Animator and cannot flicker.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WrestlerView : MonoBehaviour
    {
        /// <summary>Animator int parameter: (int)CombatState.</summary>
        public static readonly int StateParam = Animator.StringToHash("State");
        /// <summary>Animator float parameter: 0 = front-facing, 1 = back-facing (blend trees use exact 0/1).</summary>
        public static readonly int BackParam = Animator.StringToHash("Back");

        [Tooltip("Child Animator using the shared Wrestler controller (or this wrestler's override controller).")]
        [SerializeField] private Animator animator;
        [Tooltip("Child SpriteRenderer the Animator drives. Flipped when the wrestler faces right.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Which way this wrestler's source art faces. All Barry art faces left.")]
        [SerializeField] private bool artFacesLeft = true;

        private IWrestlerView view;

        /// <summary>The bound combat view, or null before <see cref="Bind"/>.</summary>
        public IWrestlerView View => view;
        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        /// <summary>
        /// Binds this presentation to one wrestler's combat view. Call once after the CombatMatch is built
        /// (MatchBootstrap, integration step). Applies the current state immediately so the first rendered
        /// frame is already correct. Passing null unbinds (the wrestler holds its last pose).
        /// </summary>
        public void Bind(IWrestlerView wrestlerView)
        {
            view = wrestlerView;
            Apply();
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // Default execution order (0) runs after CombatMatchRunner.Update (-50), so this reads the state
        // the runner just ticked, and the Animator evaluates the new parameters in the same frame.
        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (view == null)
                return;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(StateParam, (int)view.State);
                animator.SetFloat(BackParam, IsBackFacing(view.Facing) ? 1f : 0f);
            }

            if (spriteRenderer != null)
                spriteRenderer.flipX = ShouldFlip(view.HorizontalFacing, artFacesLeft);
        }

        /// <summary>Back-facing = any facing with an upward (away from camera) component (STYLE_GUIDE: Up/Left, Up, Up/Right).</summary>
        public static bool IsBackFacing(Vector2 facing) => facing.y > 0.01f;

        /// <summary>Art drawn facing left is flipped when facing right (+1), and vice versa.</summary>
        public static bool ShouldFlip(int horizontalFacing, bool artFacesLeft) =>
            artFacesLeft ? horizontalFacing > 0 : horizontalFacing < 0;
    }
}
