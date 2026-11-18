using System.Collections.Generic;
using RuralRumble.Combat;
using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>How hard a moment hits, for the D-046 theatrics (flash, blood, shake, hitstop, stars, dust).</summary>
    public enum ImpactKind
    {
        /// <summary>Combo hits 1 and 2 (and any other light move that raises HitLanded).</summary>
        LightStrike = 0,
        /// <summary>Combo finisher, hit 3.</summary>
        HeavyStrike = 1,
        /// <summary>Stomp on a downed opponent.</summary>
        Stomp = 2,
        /// <summary>Bodyslam landing (HitLanded Bodyslam).</summary>
        Slam = 3,
        /// <summary>Running or whipped into the truck or pool (ObstacleHit).</summary>
        Crash = 4,
        /// <summary>A runner met the opponent: both go down (D-029), no HitLanded.</summary>
        RunCollision = 5,
        /// <summary>A knockdown with no hit, crash or run collision on the same tick (e.g. a bodyslam without a throw ending).</summary>
        Knockdown = 6,
        /// <summary>Health went down with nothing else on the same tick (any other damage source).</summary>
        Damage = 7,
    }

    /// <summary>One receiver's impact on one combat tick.</summary>
    public readonly struct Impact
    {
        public readonly WrestlerId Receiver;
        public readonly ImpactKind Kind;
        /// <summary>World x of what hit the receiver (attacker feet, obstacle contact, other wrestler). Sprays go away from it.</summary>
        public readonly float SourceX;
        /// <summary>Contact point on the arena plane (feet depth), from Combat.</summary>
        public readonly Vector2 Contact;
        /// <summary>This impact knocked the receiver out (MatchEnded on the same tick).</summary>
        public readonly bool Knockout;

        public Impact(WrestlerId receiver, ImpactKind kind, float sourceX, Vector2 contact, bool knockout = false)
        {
            Receiver = receiver;
            Kind = kind;
            SourceX = sourceX;
            Contact = contact;
            Knockout = knockout;
        }

        public Impact AsKnockout() => new Impact(Receiver, Kind, SourceX, Contact, true);
    }

    /// <summary>
    /// Everything CombatEvents raised during one frame's combat tick(s), collected by <see cref="CombatTheatrics"/> and
    /// resolved after the tick (so a knockout raised at the end of the tick upgrades the hit that caused it).
    /// </summary>
    public sealed class TickEvents
    {
        public readonly List<HitInfo> Hits = new List<HitInfo>();
        public readonly List<KeyValuePair<WrestlerId, Vector2>> Obstacles = new List<KeyValuePair<WrestlerId, Vector2>>();
        /// <summary>Per wrestler (index = (int)WrestlerId): a KnockedDown event fired.</summary>
        public readonly bool[] KnockedDown = new bool[2];
        /// <summary>Per wrestler: StateChanged to KnockedOut.</summary>
        public readonly bool[] KnockedOut = new bool[2];
        /// <summary>Per wrestler: StateChanged from Running straight to KnockedDown or KnockedOut (a run collision's runner).</summary>
        public readonly bool[] DownFromRunning = new bool[2];
        /// <summary>Per wrestler: HealthChanged lowered health.</summary>
        public readonly bool[] Damaged = new bool[2];
        public bool HasEnded;
        public MatchResult Ended;

        public bool IsEmpty =>
            Hits.Count == 0 && Obstacles.Count == 0 && !HasEnded &&
            !KnockedDown[0] && !KnockedDown[1] && !KnockedOut[0] && !KnockedOut[1] && !Damaged[0] && !Damaged[1];

        public void Clear()
        {
            Hits.Clear();
            Obstacles.Clear();
            for (int i = 0; i < 2; i++)
                KnockedDown[i] = KnockedOut[i] = DownFromRunning[i] = Damaged[i] = false;
            HasEnded = false;
            Ended = default;
        }
    }

    /// <summary>
    /// Pure rules: turns one tick's combat events into impacts (who was hurt, how hard, from where). Presentation only.
    /// Every damage event and every knockdown / knockout gives its receiver exactly one impact (or more when several
    /// things hit on the same tick), so the red flash covers all of them (D-046).
    /// </summary>
    public static class ImpactClassifier
    {
        public static ImpactKind KindFor(MoveId move)
        {
            switch (move)
            {
                case MoveId.Strike3: return ImpactKind.HeavyStrike;
                case MoveId.Stomp: return ImpactKind.Stomp;
                case MoveId.Bodyslam: return ImpactKind.Slam;
                default: return ImpactKind.LightStrike;
            }
        }

        /// <summary>
        /// Resolves <paramref name="e"/> into <paramref name="into"/> (cleared first). <paramref name="playerPos"/> and
        /// <paramref name="cpuPos"/> are the wrestlers' feet points after the tick.
        /// </summary>
        public static void Resolve(TickEvents e, Vector2 playerPos, Vector2 cpuPos, List<Impact> into)
        {
            into.Clear();
            var covered = new bool[2];

            foreach (HitInfo hit in e.Hits)
            {
                float sourceX = PositionOf(hit.Attacker, playerPos, cpuPos).x;
                into.Add(new Impact(hit.Defender, KindFor(hit.Move), sourceX, hit.ContactPoint));
                covered[(int)hit.Defender] = true;
            }

            foreach (KeyValuePair<WrestlerId, Vector2> o in e.Obstacles)
            {
                into.Add(new Impact(o.Key, ImpactKind.Crash, o.Value.x, o.Value));
                covered[(int)o.Key] = true;
            }

            bool down0 = e.KnockedDown[0] || e.KnockedOut[0];
            bool down1 = e.KnockedDown[1] || e.KnockedOut[1];
            // D-029: a run collision knocks both down on one tick, one of them straight out of a run, with no hit or crash.
            bool runCollision = down0 && down1 && !covered[0] && !covered[1] && (e.DownFromRunning[0] || e.DownFromRunning[1]);

            for (int i = 0; i < 2; i++)
            {
                if (covered[i])
                    continue;
                var id = (WrestlerId)i;
                Vector2 self = PositionOf(id, playerPos, cpuPos);
                float otherX = PositionOf(Other(id), playerPos, cpuPos).x;
                bool down = i == 0 ? down0 : down1;
                if (down)
                    into.Add(new Impact(id, runCollision ? ImpactKind.RunCollision : ImpactKind.Knockdown, otherX, self));
                else if (e.Damaged[i])
                    into.Add(new Impact(id, ImpactKind.Damage, otherX, self));
                else
                    continue;
                covered[i] = true;
            }

            if (!e.HasEnded)
                return;

            if (e.Ended.IsDraw)
            {
                MarkKnockout(WrestlerId.Player, into, playerPos, cpuPos);
                MarkKnockout(WrestlerId.Cpu, into, playerPos, cpuPos);
            }
            else
            {
                MarkKnockout(e.Ended.Loser, into, playerPos, cpuPos);
            }
        }

        /// <summary>The knockout rides on the receiver's heaviest impact this tick; with none, a plain knockdown impact.</summary>
        private static void MarkKnockout(WrestlerId loser, List<Impact> impacts, Vector2 playerPos, Vector2 cpuPos)
        {
            int best = -1;
            for (int i = 0; i < impacts.Count; i++)
            {
                if (impacts[i].Receiver != loser)
                    continue;
                if (best < 0 || Severity(impacts[i].Kind) > Severity(impacts[best].Kind))
                    best = i;
            }
            if (best >= 0)
            {
                impacts[best] = impacts[best].AsKnockout();
                return;
            }
            impacts.Add(new Impact(loser, ImpactKind.Knockdown, PositionOf(Other(loser), playerPos, cpuPos).x,
                PositionOf(loser, playerPos, cpuPos), true));
        }

        /// <summary>Ordering used to pick which impact carries the knockout.</summary>
        public static int Severity(ImpactKind kind)
        {
            switch (kind)
            {
                case ImpactKind.Slam: return 6;
                case ImpactKind.Crash: return 5;
                case ImpactKind.RunCollision: return 5;
                case ImpactKind.HeavyStrike: return 4;
                case ImpactKind.Stomp: return 3;
                case ImpactKind.LightStrike: return 2;
                case ImpactKind.Knockdown: return 1;
                default: return 0;
            }
        }

        /// <summary>Spray direction on x (+1 / -1): away from the source; on a tie, <paramref name="fallback"/> (or +1).</summary>
        public static int AwayFrom(float receiverX, float sourceX, int fallback)
        {
            float d = receiverX - sourceX;
            if (d > 1e-4f) return 1;
            if (d < -1e-4f) return -1;
            return fallback < 0 ? -1 : 1;
        }

        private static Vector2 PositionOf(WrestlerId id, Vector2 playerPos, Vector2 cpuPos) =>
            id == WrestlerId.Player ? playerPos : cpuPos;

        private static WrestlerId Other(WrestlerId id) => id == WrestlerId.Player ? WrestlerId.Cpu : WrestlerId.Player;
    }
}
