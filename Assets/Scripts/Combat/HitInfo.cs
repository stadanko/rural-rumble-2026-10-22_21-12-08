using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>One landed hit, carried by <see cref="CombatEvents.HitLanded"/> (ARCHITECTURE 4.2).</summary>
    public readonly struct HitInfo
    {
        public readonly WrestlerId Attacker;
        public readonly WrestlerId Defender;
        public readonly MoveId Move;
        /// <summary>Health removed by this hit (after any clamping at 0).</summary>
        public readonly float Damage;
        /// <summary>Contact point on the arena plane (x = left/right, y = depth), world units.</summary>
        public readonly Vector2 ContactPoint;

        public HitInfo(WrestlerId attacker, WrestlerId defender, MoveId move, float damage, Vector2 contactPoint)
        {
            Attacker = attacker;
            Defender = defender;
            Move = move;
            Damage = damage;
            ContactPoint = contactPoint;
        }
    }
}
