using System;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// How one kind of impact feels (D-046). Presentation only; every value is a Starting point for the designer to tune
    /// in the Inspector of <see cref="CombatTheatrics"/>. Hitstop goes through the Architect's match time hook
    /// (CombatMatchRunner.RequestHitstop); nothing here changes combat rules or Time.timeScale.
    /// </summary>
    [Serializable]
    public sealed class ImpactFeel
    {
        [Tooltip("Hitstop: real seconds the combat clock and wrestler animation freeze on this impact (0 = none). Starting point.")]
        [Range(0f, 0.3f)] public float hitstopSeconds;

        [Tooltip("Screen shake strength in world units (1/60 = one art pixel). Starting point.")]
        [Range(0f, 0.6f)] public float shakeAmplitude;
        [Tooltip("Screen shake length in real seconds; it decays to nothing over this time. Starting point.")]
        [Range(0f, 1.5f)] public float shakeSeconds;

        [Tooltip("Blood drops sprayed (0 = no blood). Starting point.")]
        [Range(0, 200)] public int bloodDrops;
        [Tooltip("Top launch speed of the blood drops, world units per second. Starting point.")]
        [Range(0f, 15f)] public float bloodSpeed;
        [Tooltip("Where on the receiver the blood and impact stars start: 0 = feet, 1 = top of the current sprite. Starting point.")]
        [Range(0f, 1f)] public float contactHeight;

        [Tooltip("White pixel impact stars at the contact point (0 = none). Starting point.")]
        [Range(0, 6)] public int stars;
        [Tooltip("Size of the main impact star in art pixels; extra stars are smaller. Starting point.")]
        [Range(4f, 48f)] public float starPixels;

        [Tooltip("Dust puffs kicked up on the ground at the impact (0 = none). Knockdown falls get their own dust when they land. Starting point.")]
        [Range(0, 30)] public int dustPuffs;

        public ImpactFeel() { }

        public ImpactFeel(float hitstop, float shake, float shakeTime, int drops, float speed, float height, int starCount,
            float starPx, int dust)
        {
            hitstopSeconds = hitstop;
            shakeAmplitude = shake;
            shakeSeconds = shakeTime;
            bloodDrops = drops;
            bloodSpeed = speed;
            contactHeight = height;
            stars = starCount;
            starPixels = starPx;
            dustPuffs = dust;
        }
    }

    /// <summary>One <see cref="ImpactFeel"/> per <see cref="ImpactKind"/>, plus the knockout extras. Defaults are D-046's Starting points.</summary>
    [Serializable]
    public sealed class ImpactFeelTable
    {
        [Tooltip("Combo hits 1 and 2. D-046: small spray, light shake, about 50 ms hitstop.")]
        public ImpactFeel lightStrike = new ImpactFeel(0.05f, 0.05f, 0.14f, 18, 5f, 0.8f, 1, 16f, 0);
        [Tooltip("Combo finisher (hit 3). D-046: bigger spray, medium shake, about 90 ms hitstop.")]
        public ImpactFeel heavyStrike = new ImpactFeel(0.09f, 0.11f, 0.22f, 26, 6f, 0.75f, 2, 20f, 0);
        [Tooltip("Stomp on a downed opponent. D-046: bigger spray, medium shake, about 90 ms hitstop.")]
        public ImpactFeel stomp = new ImpactFeel(0.09f, 0.11f, 0.22f, 26, 5.5f, 0.5f, 2, 18f, 4);
        [Tooltip("Bodyslam landing. D-046: big spray, heavy shake, about 120 ms hitstop, dust cloud.")]
        public ImpactFeel slam = new ImpactFeel(0.12f, 0.2f, 0.34f, 64, 7.5f, 0.5f, 2, 24f, 12);
        [Tooltip("Run or whip into the truck or pool. D-046: big spray, heavy shake, about 100 ms hitstop, dust cloud.")]
        public ImpactFeel crash = new ImpactFeel(0.1f, 0.18f, 0.3f, 56, 7f, 0.6f, 2, 24f, 10);
        [Tooltip("Run collision (both wrestlers go down). D-046: big spray, heavy shake, about 100 ms hitstop, dust cloud.")]
        public ImpactFeel runCollision = new ImpactFeel(0.1f, 0.18f, 0.3f, 50, 6.5f, 0.6f, 1, 22f, 8);
        [Tooltip("A knockdown with nothing else on the same tick (e.g. a bodyslam without a throw ending). Flash and dust only by default.")]
        public ImpactFeel knockdown = new ImpactFeel(0f, 0.06f, 0.16f, 0, 0f, 0.5f, 0, 12f, 6);
        [Tooltip("Any other damage with nothing else on the same tick. Small spray.")]
        public ImpactFeel damage = new ImpactFeel(0.04f, 0.05f, 0.14f, 10, 4f, 0.6f, 1, 12f, 0);
        [Tooltip("Added on the knockout blow, on top of that blow's own feel: the big burst, the heaviest shake and the KO hitstop. " +
                 "Keep hitstop within the slow motion below (they run at the same time) and both under about 1.4 s: Victory loads 1.5 s real time after the knockout.")]
        public ImpactFeel knockout = new ImpactFeel(0.14f, 0.32f, 0.7f, 100, 8.5f, 0.55f, 3, 32f, 14);

        public ImpactFeel Get(ImpactKind kind)
        {
            switch (kind)
            {
                case ImpactKind.LightStrike: return lightStrike;
                case ImpactKind.HeavyStrike: return heavyStrike;
                case ImpactKind.Stomp: return stomp;
                case ImpactKind.Slam: return slam;
                case ImpactKind.Crash: return crash;
                case ImpactKind.RunCollision: return runCollision;
                case ImpactKind.Knockdown: return knockdown;
                default: return damage;
            }
        }
    }
}
