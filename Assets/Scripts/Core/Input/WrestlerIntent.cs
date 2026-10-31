using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>State of one combat button since the previous <see cref="IWrestlerInput.Read"/>.</summary>
    public readonly struct ButtonState
    {
        /// <summary>The button went down at least once since the last Read.</summary>
        public readonly bool Pressed;
        /// <summary>The button is down at the moment of this Read.</summary>
        public readonly bool Held;

        public ButtonState(bool pressed, bool held)
        {
            Pressed = pressed;
            Held = held;
        }

        public static ButtonState None => default;
    }

    /// <summary>
    /// One tick of raw wrestler input (ARCHITECTURE 4.1). Buttons, not actions:
    /// Combat decides what a press means in the current state.
    /// </summary>
    public readonly struct WrestlerIntent
    {
        /// <summary>Raw stick/D-pad/WASD, each axis -1..1. Combat quantizes to 8 directions.</summary>
        public readonly Vector2 Move;
        /// <summary>A / K.</summary>
        public readonly ButtonState LockUpGrapple;
        /// <summary>B / Space.</summary>
        public readonly ButtonState RunWhip;
        /// <summary>X / J.</summary>
        public readonly ButtonState Strike;

        public WrestlerIntent(Vector2 move, ButtonState lockUpGrapple, ButtonState runWhip, ButtonState strike)
        {
            Move = new Vector2(Mathf.Clamp(move.x, -1f, 1f), Mathf.Clamp(move.y, -1f, 1f));
            LockUpGrapple = lockUpGrapple;
            RunWhip = runWhip;
            Strike = strike;
        }

        public static WrestlerIntent None => default;
    }
}
