using System.Collections.Generic;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Match sound effects (ARCHITECTURE 4.6, D-021). Lives in Match.unity; listens to the running match's
    /// <see cref="CombatEvents"/> and plays clips through its own pooled 2D AudioSources. Presentation only:
    /// never changes combat state, positions, health, timing or Time.timeScale. Empty clip slots and missing
    /// voice entries are skipped silently. Decisions live in <see cref="CombatSoundRules"/> and
    /// <see cref="BodyslamThudFilter"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAudio : MonoBehaviour
    {
        [Header("Strikes")]
        [Tooltip("Punch/kick combo hit 1 (sfx_strike1).")]
        [SerializeField] private AudioClipSlot strike1 = new AudioClipSlot(1f);
        [Tooltip("Combo hit 2 (sfx_strike2).")]
        [SerializeField] private AudioClipSlot strike2 = new AudioClipSlot(1f);
        [Tooltip("Combo finisher, hit 3 (sfx_strike3).")]
        [SerializeField] private AudioClipSlot strike3 = new AudioClipSlot(1f);
        [Tooltip("Stomp on a downed opponent (sfx_stomp).")]
        [SerializeField] private AudioClipSlot stomp = new AudioClipSlot(1f);

        [Header("Body and arena")]
        [Tooltip("Body hitting the ground (sfx_body_thud): bodyslam impact, knockdown, knockout fall.")]
        [SerializeField] private AudioClipSlot bodyThud = new AudioClipSlot(1f);
        [Tooltip("Running into the truck (sfx_impact_truck).")]
        [SerializeField] private AudioClipSlot truckImpact = new AudioClipSlot(1f);
        [Tooltip("Running into the pool (sfx_impact_pool). The clip is quiet; keep the volume high.")]
        [SerializeField] private AudioClipSlot poolImpact = new AudioClipSlot(1f);

        [Header("Voices")]
        [Tooltip("Per-wrestler grunt and knockout groan. A wrestler with no entry stays silent.")]
        [SerializeField] private List<WrestlerVoice> voices = new List<WrestlerVoice>();

        [Header("Obstacles")]
        [Tooltip("The truck's ArenaObstacleMarker (Arena prefab instance). Used only to tell truck from pool by distance to the contact point.")]
        [SerializeField] private ArenaObstacleMarker truckMarker;
        [Tooltip("The pool's ArenaObstacleMarker (Arena prefab instance).")]
        [SerializeField] private ArenaObstacleMarker poolMarker;

        [Header("Variation")]
        [Tooltip("Chance (0 to 1) that combo hits 1 and 2 make the defender grunt. Hit 3, stomp, bodyslam and obstacle crashes always do.")]
        [Range(0f, 1f)] [SerializeField] private float lightHitGruntChance = 0.35f;
        [Tooltip("Random pitch spread for grunts and groans: pitch is 1 ± this value.")]
        [Range(0f, 0.5f)] [SerializeField] private float gruntPitchVariation = 0.06f;
        [Tooltip("Random pitch spread for strikes, thuds and obstacle impacts: pitch is 1 ± this value.")]
        [Range(0f, 0.5f)] [SerializeField] private float impactPitchVariation = 0.03f;

        [Header("Advanced")]
        [Tooltip("Seconds after a bodyslam impact during which that wrestler's knockdown plays no second thud. Impact to knockdown is about 1.2 s.")]
        [Min(0f)] [SerializeField] private float bodyslamThudWindow = 2f;
        [Tooltip("How many sounds can play at once. Read when the match starts.")]
        [Range(1, 16)] [SerializeField] private int simultaneousSounds = 8;

        private struct PendingGrunt
        {
            public WrestlerId Wrestler;
            public MoveId Move;
            public bool Obstacle;
            public float Roll;
        }

        private CombatMatch match;
        private CombatEvents events;
        private MatchSetup setup;
        private AudioSourcePool pool;
        private BodyslamThudFilter thudFilter;
        private readonly bool[] knockedOut = new bool[2];
        private readonly List<PendingGrunt> pendingGrunts = new List<PendingGrunt>();

        private float Now => match != null ? match.ElapsedSeconds : Time.time;

        private void Awake()
        {
            pool = new AudioSourcePool(gameObject, simultaneousSounds);
            thudFilter = new BodyslamThudFilter(bodyslamThudWindow);
        }

        private void Start()
        {
            MatchBootstrap bootstrap = MatchBootstrap.Current;
            match = bootstrap != null ? bootstrap.Match : null;
            if (match == null)
            {
                Debug.LogWarning("CombatAudio: no running match (MatchBootstrap.Current.Match); combat sounds are off.", this);
                return;
            }

            setup = bootstrap.Setup;
            events = match.Events;
            events.HitLanded += OnHitLanded;
            events.ObstacleHit += OnObstacleHit;
            events.KnockedDown += OnKnockedDown;
            events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (events == null)
                return;
            events.HitLanded -= OnHitLanded;
            events.ObstacleHit -= OnObstacleHit;
            events.KnockedDown -= OnKnockedDown;
            events.MatchEnded -= OnMatchEnded;
            events = null;
        }

        // Grunts wait until after the combat tick (Update), so a knockout raised later in the same tick
        // (MatchEnded) can cancel them: the knockout groan replaces the grunt.
        private void LateUpdate()
        {
            if (pendingGrunts.Count == 0)
                return;

            var grunted = new bool[2];
            foreach (PendingGrunt g in pendingGrunts)
            {
                bool ko = knockedOut[(int)g.Wrestler];
                bool grunt = g.Obstacle ? !ko : CombatSoundRules.ShouldGrunt(g.Move, ko, g.Roll, lightHitGruntChance);
                if (!grunt)
                    continue;
                // At most one grunt per wrestler per frame.
                if (grunted[(int)g.Wrestler])
                    continue;
                grunted[(int)g.Wrestler] = true;
                WrestlerVoice voice = VoiceFor(g.Wrestler);
                if (voice != null)
                    pool.Play(voice.hurt, voice.volume, RandomPitch(gruntPitchVariation));
            }
            pendingGrunts.Clear();
        }

        private void OnHitLanded(HitInfo hit)
        {
            PlayImpact(SlotFor(CombatSoundRules.ImpactFor(hit.Move)));
            if (hit.Move == MoveId.Bodyslam)
                thudFilter.NoteBodyslamImpact(hit.Defender, Now);
            pendingGrunts.Add(new PendingGrunt { Wrestler = hit.Defender, Move = hit.Move, Roll = Random.value });
        }

        private void OnObstacleHit(WrestlerId wrestler, Vector2 contactPoint)
        {
            ArenaRect? truckRect = truckMarker != null ? truckMarker.WorldRect : (ArenaRect?)null;
            ArenaRect? poolRect = poolMarker != null ? poolMarker.WorldRect : (ArenaRect?)null;
            switch (CombatSoundRules.NearestObstacle(contactPoint, truckRect, poolRect))
            {
                case ObstacleSound.Truck: PlayImpact(truckImpact); break;
                case ObstacleSound.Pool: PlayImpact(poolImpact); break;
            }
            pendingGrunts.Add(new PendingGrunt { Wrestler = wrestler, Obstacle = true });
        }

        private void OnKnockedDown(WrestlerId wrestler, float duration)
        {
            thudFilter.WindowSeconds = bodyslamThudWindow;
            if (thudFilter.ShouldThudOnKnockdown(wrestler, Now))
                PlayImpact(bodyThud);
        }

        private void OnMatchEnded(MatchResult result)
        {
            float now = Now;
            bool thud = false;
            if (result.IsDraw)
            {
                thud |= KnockOut(WrestlerId.Player, now);
                thud |= KnockOut(WrestlerId.Cpu, now);
            }
            else
            {
                thud = KnockOut(result.Loser, now);
            }
            // Knockout falls raise no KnockedDown, so the fall's thud plays here (once, even on a draw).
            if (thud)
                PlayImpact(bodyThud);
        }

        /// <summary>Marks the wrestler knocked out and plays the groan. Returns whether the fall should thud.</summary>
        private bool KnockOut(WrestlerId wrestler, float now)
        {
            knockedOut[(int)wrestler] = true;
            WrestlerVoice voice = VoiceFor(wrestler);
            if (voice != null)
                pool.Play(voice.knockout, voice.volume, RandomPitch(gruntPitchVariation));
            return thudFilter.ShouldThudOnKnockout(wrestler, now);
        }

        private AudioClipSlot SlotFor(ImpactSound sound)
        {
            switch (sound)
            {
                case ImpactSound.Strike1: return strike1;
                case ImpactSound.Strike2: return strike2;
                case ImpactSound.Strike3: return strike3;
                case ImpactSound.Stomp: return stomp;
                case ImpactSound.BodyThud: return bodyThud;
                default: return null;
            }
        }

        private void PlayImpact(AudioClipSlot slot) => pool.Play(slot, RandomPitch(impactPitchVariation));

        private WrestlerVoice VoiceFor(WrestlerId wrestler) =>
            CombatSoundRules.FindVoice(voices, wrestler == WrestlerId.Player ? setup.Player : setup.Cpu);

        private static float RandomPitch(float variation) => CombatSoundRules.Pitch(variation, Random.value);
    }
}
