using System.Collections.Generic;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>Which impact sound a landed hit plays.</summary>
    public enum ImpactSound
    {
        None,
        Strike1,
        Strike2,
        Strike3,
        Stomp,
        BodyThud
    }

    /// <summary>Which arena object a wrestler ran into.</summary>
    public enum ObstacleSound
    {
        None,
        Truck,
        Pool
    }

    /// <summary>
    /// Sound decisions for <see cref="CombatAudio"/>, kept pure for EditMode tests. Presentation only:
    /// nothing here reads or changes combat rules.
    /// </summary>
    public static class CombatSoundRules
    {
        /// <summary>The impact sound for a landed move. Bodyslam plays the body thud at its impact.</summary>
        public static ImpactSound ImpactFor(MoveId move)
        {
            switch (move)
            {
                case MoveId.Strike1: return ImpactSound.Strike1;
                case MoveId.Strike2: return ImpactSound.Strike2;
                case MoveId.Strike3: return ImpactSound.Strike3;
                case MoveId.Stomp: return ImpactSound.Stomp;
                case MoveId.Bodyslam: return ImpactSound.BodyThud;
                default: return ImpactSound.None;
            }
        }

        /// <summary>
        /// Whether the defender grunts after a landed hit (designer decision): always on Strike 3, Stomp and
        /// Bodyslam; on Strike 1 and 2 only when <paramref name="roll"/> (0..1) is below
        /// <paramref name="lightHitChance"/> (a chance of 1 always grunts, 0 never). Never when the hit
        /// knocked the defender out: the knockout groan covers it.
        /// </summary>
        public static bool ShouldGrunt(MoveId move, bool defenderKnockedOut, float roll, float lightHitChance)
        {
            if (defenderKnockedOut)
                return false;
            switch (move)
            {
                case MoveId.Strike3:
                case MoveId.Stomp:
                case MoveId.Bodyslam:
                    return true;
                case MoveId.Strike1:
                case MoveId.Strike2:
                    return lightHitChance >= 1f || roll < lightHitChance;
                default:
                    return false;
            }
        }

        /// <summary>Pitch for one playback: 1 ± <paramref name="variation"/>, spread by <paramref name="roll"/> (0..1).</summary>
        public static float Pitch(float variation, float roll)
        {
            float v = Mathf.Max(0f, variation);
            return 1f + Mathf.Lerp(-v, v, Mathf.Clamp01(roll));
        }

        /// <summary>
        /// The obstacle nearest the contact point (distance to each rect, 0 inside it). A missing rect is
        /// ignored; with neither, <see cref="ObstacleSound.None"/>. A tie goes to the truck.
        /// </summary>
        public static ObstacleSound NearestObstacle(Vector2 point, ArenaRect? truck, ArenaRect? pool)
        {
            if (truck == null && pool == null)
                return ObstacleSound.None;
            if (pool == null)
                return ObstacleSound.Truck;
            if (truck == null)
                return ObstacleSound.Pool;
            return DistanceSq(point, truck.Value) <= DistanceSq(point, pool.Value) ? ObstacleSound.Truck : ObstacleSound.Pool;
        }

        /// <summary>The voice entry for this wrestler, or null when there is none (stay silent).</summary>
        public static WrestlerVoice FindVoice(IReadOnlyList<WrestlerVoice> voices, WrestlerDefinition wrestler)
        {
            if (voices == null || wrestler == null)
                return null;
            for (int i = 0; i < voices.Count; i++)
            {
                WrestlerVoice v = voices[i];
                if (v != null && v.wrestler == wrestler)
                    return v;
            }
            return null;
        }

        private static float DistanceSq(Vector2 p, ArenaRect r)
        {
            float dx = Mathf.Max(r.XMin - p.x, 0f, p.x - r.XMax);
            float dy = Mathf.Max(r.YMin - p.y, 0f, p.y - r.YMax);
            return dx * dx + dy * dy;
        }
    }
}
