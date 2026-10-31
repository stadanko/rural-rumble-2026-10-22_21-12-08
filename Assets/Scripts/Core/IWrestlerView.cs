using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Read-only view of one wrestler. Defined in Core, implemented by Combat, read by presentation,
    /// UI, AI, Juice and the camera (ARCHITECTURE 4.2). Readers never change combat state.
    /// </summary>
    public interface IWrestlerView
    {
        WrestlerId Id { get; }
        CombatState State { get; }
        MoveId CurrentMove { get; }
        MovePhase Phase { get; }
        float Health { get; }
        float MaxHealth { get; }
        /// <summary>Arena plane: x = left/right, y = depth. Same units as world space (60 px per unit).</summary>
        Vector2 Position { get; }
        /// <summary>Last 8-way direction. Presentation derives left/right and front/back from it.</summary>
        Vector2 Facing { get; }
        /// <summary>
        /// Last left/right facing: +1 right, -1 left. Kept when Facing is straight up or down, so
        /// presentation can flip the sprite and a run started while facing up/down goes this way (D-020).
        /// </summary>
        int HorizontalFacing { get; }
    }
}
