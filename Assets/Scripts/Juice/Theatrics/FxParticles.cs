using System.Collections.Generic;
using RuralRumble.Combat;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>One effect particle: a blood drop, impact star or dust puff (D-046). Plain data, simulated by <see cref="FxParticleList"/>.</summary>
    public struct FxParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        /// <summary>World units per second squared, pulling down (-y).</summary>
        public float Gravity;
        /// <summary>Fraction of velocity lost per second (0 = none).</summary>
        public float Drag;
        /// <summary>When true the particle stops at <see cref="GroundY"/> while falling and is reported as landed.</summary>
        public bool Lands;
        public float GroundY;
        public float Age;
        public float Lifetime;
        /// <summary>World size (square) at birth and at the end of its life.</summary>
        public float StartSize;
        public float EndSize;
        public Color32 Color;
        /// <summary>Fraction of the lifetime after which the alpha fades linearly to 0 (1 = never fades).</summary>
        public float FadeFrom;
        /// <summary>Drawn in the "behind the props" batches: its wrestler stands behind the truck or pool.</summary>
        public bool Behind;

        public float Life01 => Lifetime > 0f ? Mathf.Clamp01(Age / Lifetime) : 1f;
        public float Size => Mathf.Lerp(StartSize, EndSize, Life01);

        public float Alpha01
        {
            get
            {
                float t = Life01;
                if (t <= FadeFrom || FadeFrom >= 1f)
                    return 1f;
                return Mathf.Clamp01(1f - (t - FadeFrom) / (1f - FadeFrom));
            }
        }
    }

    /// <summary>
    /// Pure simulation of a capped list of <see cref="FxParticle"/>s: gravity, drag, landing on a ground line, expiry.
    /// When full, a new particle replaces the oldest.
    /// </summary>
    public sealed class FxParticleList
    {
        private readonly List<FxParticle> items;
        private readonly int capacity;

        public FxParticleList(int capacity)
        {
            this.capacity = Mathf.Max(1, capacity);
            items = new List<FxParticle>(this.capacity);
        }

        public int Count => items.Count;
        public int Capacity => capacity;
        public FxParticle this[int i] => items[i];

        public void Add(FxParticle p)
        {
            if (items.Count >= capacity)
                items.RemoveAt(0);
            items.Add(p);
        }

        public void Clear() => items.Clear();

        /// <summary>Advances every particle by <paramref name="dt"/>. Landed particles are removed and added to <paramref name="landed"/> (may be null).</summary>
        public void Step(float dt, List<FxParticle> landed)
        {
            if (dt <= 0f)
                return;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                FxParticle p = items[i];
                p.Age += dt;
                p.Velocity.y -= p.Gravity * dt;
                if (p.Drag > 0f)
                    p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Position += p.Velocity * dt;

                if (p.Lands && p.Velocity.y < 0f && p.Position.y <= p.GroundY)
                {
                    p.Position.y = p.GroundY;
                    landed?.Add(p);
                    items.RemoveAt(i);
                    continue;
                }
                if (p.Age >= p.Lifetime)
                {
                    items.RemoveAt(i);
                    continue;
                }
                items[i] = p;
            }
        }
    }

    /// <summary>Pure helpers for blood sprays (D-046): launch velocity, ground line, stain placement.</summary>
    public static class BloodSpray
    {
        /// <summary>
        /// Launch velocity for one drop sprayed toward <paramref name="direction"/> (+1 / -1 on x). <paramref name="speed01"/>,
        /// <paramref name="angle01"/> and <paramref name="back01"/> are random values 0..1: speed from 35% to 100% of
        /// <paramref name="speed"/>, angle above the horizontal from <paramref name="minAngle"/> to <paramref name="maxAngle"/>
        /// degrees, and a <paramref name="backChance"/> chance of splashing the other way.
        /// </summary>
        public static Vector2 Velocity(int direction, float speed, float minAngle, float maxAngle, float backChance,
            float speed01, float angle01, float back01)
        {
            int dir = direction < 0 ? -1 : 1;
            if (back01 < backChance)
                dir = -dir;
            float a = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(angle01)) * Mathf.Deg2Rad;
            float s = speed * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(speed01));
            return new Vector2(dir * Mathf.Cos(a) * s, Mathf.Sin(a) * s);
        }

        /// <summary>
        /// The ground line a drop lands on: the receiver's feet depth plus a depth jitter, kept inside the walkable band and
        /// at least <paramref name="minDrop"/> below the launch point (so a drop from a lying body still arcs before landing).
        /// </summary>
        public static float GroundY(float feetY, float jitter, float startY, float minDrop, ArenaRect walkable)
        {
            float y = Mathf.Clamp(feetY + jitter, walkable.YMin, walkable.YMax);
            float highest = startY - Mathf.Max(0f, minDrop);
            if (y > highest)
                y = Mathf.Max(walkable.YMin, highest);
            return y;
        }

        /// <summary>
        /// Where a landed drop leaves a stain: inside the walkable band (x and y clamped). Returns false when the point lies
        /// inside an obstacle footprint (the truck or pool would hide it), so no stain is made.
        /// </summary>
        public static bool TryPlaceStain(Vector2 landed, ArenaRect walkable, IReadOnlyList<ArenaRect> obstacles, out Vector2 stain)
        {
            stain = new Vector2(Mathf.Clamp(landed.x, walkable.XMin, walkable.XMax), Mathf.Clamp(landed.y, walkable.YMin, walkable.YMax));
            if (obstacles != null)
            {
                foreach (ArenaRect r in obstacles)
                {
                    if (stain.x >= r.XMin && stain.x <= r.XMax && stain.y >= r.YMin && stain.y <= r.YMax)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether a wrestler with feet at <paramref name="feet"/> stands behind an obstacle sprite: farther back than the
        /// obstacle's front edge (its footprint's YMin, where the sprite's sort pivot sits, D-035) and within its x range
        /// widened by <paramref name="xMargin"/> (a lying body or a spray reaches past the feet). Effects for that wrestler draw
        /// under the props so the truck or pool still covers them.
        /// </summary>
        public static bool IsBehindObstacle(Vector2 feet, IReadOnlyList<ArenaRect> obstacles, float xMargin)
        {
            if (obstacles == null)
                return false;
            foreach (ArenaRect r in obstacles)
            {
                if (feet.y > r.YMin && feet.x >= r.XMin - xMargin && feet.x <= r.XMax + xMargin)
                    return true;
            }
            return false;
        }
    }
}
