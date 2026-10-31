using UnityEngine;
using UnityEngine.InputSystem;

namespace RuralRumble.Core
{
    /// <summary>
    /// Reads the "Wrestler" map of the Input Actions asset and produces <see cref="WrestlerIntent"/>s.
    /// The only place gameplay input touches devices (ARCHITECTURE 4.1).
    /// A press is remembered until the next Read, so no press is lost between combat ticks.
    /// </summary>
    public sealed class PlayerInputSource : MonoBehaviour, IWrestlerInput
    {
        public const string MapName = "Wrestler";

        [Tooltip("Input Actions asset. Leave empty to use the project-wide actions (InputSystem_Actions).")]
        [SerializeField] private InputActionAsset actions;

        private InputActionMap map;
        private InputAction move;
        private PressLatch lockUpGrapple;
        private PressLatch runWhip;
        private PressLatch strike;

        private void OnEnable()
        {
            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogError("PlayerInputSource: no Input Actions asset assigned and no project-wide actions set.", this);
                return;
            }

            map = asset.FindActionMap(MapName, throwIfNotFound: true);
            move = map.FindAction("Move", throwIfNotFound: true);
            lockUpGrapple = new PressLatch(map.FindAction("LockUpGrapple", throwIfNotFound: true));
            runWhip = new PressLatch(map.FindAction("RunWhip", throwIfNotFound: true));
            strike = new PressLatch(map.FindAction("Strike", throwIfNotFound: true));
            map.Enable();
        }

        private void OnDisable()
        {
            lockUpGrapple?.Dispose();
            runWhip?.Dispose();
            strike?.Dispose();
            lockUpGrapple = runWhip = strike = null;
            map = null;
            move = null;
        }

        public WrestlerIntent Read()
        {
            if (map == null)
                return WrestlerIntent.None;

            return new WrestlerIntent(
                move.ReadValue<Vector2>(),
                lockUpGrapple.Consume(),
                runWhip.Consume(),
                strike.Consume());
        }

        private sealed class PressLatch : System.IDisposable
        {
            private readonly InputAction action;
            private bool pressedSinceRead;

            public PressLatch(InputAction action)
            {
                this.action = action;
                action.performed += OnPerformed;
            }

            private void OnPerformed(InputAction.CallbackContext _) => pressedSinceRead = true;

            public ButtonState Consume()
            {
                var state = new ButtonState(pressedSinceRead, action.IsPressed());
                pressedSinceRead = false;
                return state;
            }

            public void Dispose() => action.performed -= OnPerformed;
        }
    }
}
