using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// The one shared combat tuning asset (ARCHITECTURE 5, D-005). Both wrestlers and the CPU read
    /// from the same instance. Rules read fields at the moment of use, so Inspector edits in Play Mode
    /// apply on the next tick. Field initializers are the defaults for new instances only; never reset
    /// the project asset's tuned values.
    /// Movement slice: only movement values exist so far. The remaining COMBAT_SPEC Tuning Values are
    /// added with the combat-core slice.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Rural Rumble/Combat Tuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("Health and Damage")]
        [Tooltip("COMBAT_SPEC: Set. Full health bar.")]
        [Min(1f)] public float maxHealth = 100f;

        [Header("Movement (starting points, need designer review)")]
        [Tooltip("Walking speed left/right, world units per second (60 px = 1 unit).")]
        [Min(0f)] public float walkSpeedHorizontal = 3.0f;
        [Tooltip("Walking speed toward/away from the camera (depth), world units per second. Slower than horizontal, like classic brawlers.")]
        [Min(0f)] public float walkSpeedDepth = 2.0f;
        [Tooltip("Running speed left/right, world units per second.")]
        [Min(0f)] public float runSpeed = 7.0f;
        [Tooltip("Stick magnitude below which Move counts as no direction.")]
        [Range(0f, 0.95f)] public float moveDeadzone = 0.3f;

        [Header("Feet box (starting points, need designer review)")]
        [Tooltip("Half the width of the wrestler's feet box, world units. Keeps the feet this far from arena edges and obstacles.")]
        [Min(0f)] public float feetHalfWidth = 0.5f;
        [Tooltip("Half the depth of the wrestler's feet box, world units. 0 = the feet point can reach the band edges exactly.")]
        [Min(0f)] public float feetHalfDepth = 0f;

        [Header("Tick")]
        [Tooltip("Largest dt one combat tick may advance (D-021 capped step), seconds.")]
        [Min(0.001f)] public float maxTickStep = 0.05f;
    }
}
