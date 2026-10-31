using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// The one shared combat tuning asset (ARCHITECTURE 5, D-005). Both wrestlers and the CPU read
    /// from the same instance. Rules read fields at the moment of use, so Inspector edits in Play Mode
    /// apply on the next tick. Field initializers are the defaults for new instances only; never reset
    /// the project asset's tuned values.
    /// Tooltips say where each default comes from: "Set" / "Starting point" = COMBAT_SPEC Tuning Values
    /// (with the decision that set it); "Combat starting point" = not in COMBAT_SPEC, chosen by Combat,
    /// needs designer review. Lock-up, bodyslam and Irish Whip values come with the lock-up slice.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuning", menuName = "Rural Rumble/Combat Tuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        // ------------------------------------------------------------------ Timing
        [Header("Timing")]
        [Tooltip("COMBAT_SPEC Set (D-005). Seconds after a strike is thrown during which a Strike press continues the combo. Presses during the current strike are buffered.")]
        [Min(0f)] public float comboWindow = 0.5f;
        [Tooltip("COMBAT_SPEC Set (D-024). Seconds a wrestler stays Struck; each new hit restarts it.")]
        [Min(0f)] public float struckDuration = 0.4f;
        [Tooltip("COMBAT_SPEC Starting point (D-024). Strike 1 wind-up / hit window / recovery, seconds.")]
        public MoveTiming strike1Timing = new MoveTiming(0.21f, 0.1f, 0.15f);
        [Tooltip("COMBAT_SPEC Starting point (D-024). Strike 2 wind-up / hit window / recovery, seconds.")]
        public MoveTiming strike2Timing = new MoveTiming(0.21f, 0.1f, 0.15f);
        [Tooltip("COMBAT_SPEC Starting point (D-024). Strike 3 (kick) wind-up / hit window / recovery, seconds.")]
        public MoveTiming strike3Timing = new MoveTiming(0.125f, 0.1f, 0.25f);
        [Tooltip("Combat starting point (no art yet). Stomp wind-up / hit window / recovery, seconds.")]
        public MoveTiming stompTiming = new MoveTiming(0.2f, 0.1f, 0.3f);

        // ------------------------------------------------------------------ Health and Damage
        [Header("Health and Damage")]
        [Tooltip("COMBAT_SPEC: Set. Full health bar.")]
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("COMBAT_SPEC Set (D-004). Below this health a wrestler is Recovering, and any strike knocks down (checked after the damage, D-029).")]
        [Min(0f)] public float recoveringThreshold = 30f;
        [Tooltip("COMBAT_SPEC Starting point. Strike 1 damage.")]
        [Min(0f)] public float strike1Damage = 3f;
        [Tooltip("COMBAT_SPEC Starting point. Strike 2 damage.")]
        [Min(0f)] public float strike2Damage = 4f;
        [Tooltip("COMBAT_SPEC Starting point. Strike 3 damage (always knocks down).")]
        [Min(0f)] public float strike3Damage = 6f;
        [Tooltip("COMBAT_SPEC Starting point (D-025). Stomp damage on a Knocked Down opponent.")]
        [Min(0f)] public float stompDamage = 3f;
        [Tooltip("COMBAT_SPEC Set (D-025). Seconds after a stomp ends before the same wrestler can stomp again.")]
        [Min(0f)] public float stompCooldown = 1.0f;
        [Tooltip("COMBAT_SPEC Starting point (D-027). Damage to a runner who runs into the truck or pool.")]
        [Min(0f)] public float obstacleCollisionDamage = 8f;
        [Tooltip("COMBAT_SPEC Starting point (D-029). Damage to each wrestler when a runner runs into the opponent (both are knocked down).")]
        [Min(0f)] public float runCollisionDamage = 0f;

        // ------------------------------------------------------------------ Recovery and Knockdown
        [Header("Recovery and Knockdown")]
        [Tooltip("COMBAT_SPEC Starting point. Health regained per second while standing still in Recovering.")]
        [Min(0f)] public float recoveryRate = 1f;
        [Tooltip("COMBAT_SPEC Set (D-006). Recovering regeneration stops here; reaching it returns the wrestler to Standing.")]
        [Min(0f)] public float recoveryCap = 30f;
        [Tooltip("COMBAT_SPEC Starting point (D-025). Knockdown length at full health, seconds.")]
        [Min(0f)] public float minKnockdownTime = 1.0f;
        [Tooltip("COMBAT_SPEC Starting point (D-025). Knockdown length near 0 health, seconds. Linear in health lost between min and max.")]
        [Min(0f)] public float maxKnockdownTime = 3.0f;
        [Tooltip("COMBAT_SPEC Starting point. Share of the remaining knockdown time removed per Strike or Grapple press (0.08 = 8%).")]
        [Range(0f, 1f)] public float mashReductionPerPress = 0.08f;
        [Tooltip("COMBAT_SPEC Starting point. Mashing cannot make a knockdown shorter than this, seconds.")]
        [Min(0f)] public float shortestKnockdown = 0.5f;

        // ------------------------------------------------------------------ Movement
        [Header("Movement")]
        [Tooltip("COMBAT_SPEC Set (D-029). Walking speed straight left/right, world units per second (60 px = 1 unit).")]
        [Min(0f)] public float walkSpeedHorizontal = 3.0f;
        [Tooltip("COMBAT_SPEC Set (D-029): the 'vertical' walk speed. Walking straight toward/away from the camera, world units per second.")]
        [Min(0f)] public float walkSpeedDepth = 2.0f;
        [Tooltip("COMBAT_SPEC Set (D-029). Walking speed on diagonals, world units per second. The direction of travel follows the horizontal:vertical speed ratio; this value sets how fast.")]
        [Min(0f)] public float walkSpeedDiagonal = 3.6f;
        [Tooltip("Combat starting point. Recovering walk speed = normal walk speed x this.")]
        [Range(0f, 1f)] public float recoveringWalkSpeedMultiplier = 0.5f;
        [Tooltip("Movement-slice starting point. Running speed left/right, world units per second.")]
        [Min(0f)] public float runSpeed = 7.0f;
        [Tooltip("Stick magnitude below which Move counts as no direction.")]
        [Range(0f, 0.95f)] public float moveDeadzone = 0.3f;

        // ------------------------------------------------------------------ Feet box and blocking
        [Header("Feet box and wrestler blocking")]
        [Tooltip("Movement-slice starting point. Half the width of the wrestler's feet box, world units. Used against arena edges/obstacles and the other wrestler.")]
        [Min(0f)] public float feetHalfWidth = 0.5f;
        [Tooltip("Movement-slice starting point. Half the depth of the feet box against arena edges and obstacles. 0 = the feet point can reach the band edges exactly.")]
        [Min(0f)] public float feetHalfDepth = 0f;
        [Tooltip("Combat starting point (D-027). Half the depth of the feet box used only between the two wrestlers: they block each other when their depths are closer than twice this.")]
        [Min(0f)] public float wrestlerBlockHalfDepth = 0.25f;

        // ------------------------------------------------------------------ Hit boxes
        [Header("Hit and hurt boxes (Combat starting points)")]
        [Tooltip("How far Strike 1's hit box reaches in front of the attacker's feet point, world units (punch art ~2.4).")]
        [Min(0f)] public float strike1Reach = 2.4f;
        [Tooltip("How far Strike 2's hit box reaches in front of the attacker's feet point, world units (punch art ~2.4).")]
        [Min(0f)] public float strike2Reach = 2.4f;
        [Tooltip("How far Strike 3's hit box reaches in front of the attacker's feet point, world units (kick art ~1.6).")]
        [Min(0f)] public float strike3Reach = 1.6f;
        [Tooltip("Half the width of a wrestler's hurt box around its feet point, world units.")]
        [Min(0f)] public float hurtHalfWidth = 0.7f;
        [Tooltip("A strike connects only if the two wrestlers' depths differ by at most this, world units.")]
        [Min(0f)] public float strikeDepthTolerance = 0.4f;
        [Tooltip("'Standing over' a Knocked Down opponent (D-025): horizontal distance between feet points at most this, world units.")]
        [Min(0f)] public float stompRangeX = 2.0f;
        [Tooltip("'Standing over' a Knocked Down opponent (D-025): depth distance at most this, world units.")]
        [Min(0f)] public float stompRangeDepth = 0.5f;

        // ------------------------------------------------------------------ CPU
        [Header("CPU opponent")]
        [Tooltip("COMBAT_SPEC Starting point (D-025). Strike presses per second the CPU mashes while Knocked Down.")]
        [Min(0f)] public float cpuMashRate = 3f;
        [Tooltip("Combat starting point. Seconds between CPU decisions (GDD: decide on combat intervals, not every frame).")]
        [Min(0.01f)] public float cpuDecisionInterval = 0.25f;
        [Tooltip("Combat starting point. Chance per decision that an idle CPU starts approaching the player.")]
        [Range(0f, 1f)] public float cpuApproachChance = 0.85f;
        [Tooltip("Combat starting point. Chance per decision that a CPU in striking range throws Strike 1.")]
        [Range(0f, 1f)] public float cpuAttackChance = 0.8f;
        [Tooltip("Combat starting point. Chance per decision that the CPU presses Strike again to continue its combo.")]
        [Range(0f, 1f)] public float cpuComboContinueChance = 0.75f;
        [Tooltip("Combat starting point. Chance per decision that the CPU goes for (and throws) a stomp on a Knocked Down player.")]
        [Range(0f, 1f)] public float cpuStompChance = 0.6f;
        [Tooltip("Combat starting point. Chance per decision that a Recovering CPU strikes back at a player in range instead of standing still to regenerate. 0 = always finish recovering first.")]
        [Range(0f, 1f)] public float cpuRecoverCounterChance = 0.3f;
        [Tooltip("Combat starting point. Horizontal distance (feet to feet) the CPU walks to before striking, world units.")]
        [Min(0f)] public float cpuStrikeDistance = 1.6f;
        [Tooltip("Combat starting point. How close (world units) the CPU must get to its approach point before it stops walking.")]
        [Min(0.01f)] public float cpuArriveTolerance = 0.2f;

        // ------------------------------------------------------------------ Tick
        [Header("Tick")]
        [Tooltip("Largest dt one combat tick may advance (D-021 capped step), seconds.")]
        [Min(0.001f)] public float maxTickStep = 0.05f;

        /// <summary>Timing for a move, read now (so Play Mode edits apply on the next move).</summary>
        public MoveTiming TimingFor(MoveId move)
        {
            switch (move)
            {
                case MoveId.Strike1: return strike1Timing;
                case MoveId.Strike2: return strike2Timing;
                case MoveId.Strike3: return strike3Timing;
                case MoveId.Stomp: return stompTiming;
                default: return default;
            }
        }

        /// <summary>Damage for a move, read now.</summary>
        public float DamageFor(MoveId move)
        {
            switch (move)
            {
                case MoveId.Strike1: return strike1Damage;
                case MoveId.Strike2: return strike2Damage;
                case MoveId.Strike3: return strike3Damage;
                case MoveId.Stomp: return stompDamage;
                default: return 0f;
            }
        }

        /// <summary>Hit box reach in front of the feet point for a combo strike, read now.</summary>
        public float ReachFor(MoveId move)
        {
            switch (move)
            {
                case MoveId.Strike1: return strike1Reach;
                case MoveId.Strike2: return strike2Reach;
                case MoveId.Strike3: return strike3Reach;
                default: return 0f;
            }
        }
    }
}
