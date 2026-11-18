using System.Collections.Generic;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// D-046 theatrics ("an over the top trailer park ass beating"): red flash, blood sprays and ground stains, impact
    /// stars, dust, screen shake, hitstop, and the slow-motion knockout with a camera punch-in. Lives in Match.unity
    /// (prefab <c>Assets/Prefabs/Effects/CombatTheatrics.prefab</c>). Presentation only, driven by
    /// <see cref="CombatEvents"/> like <see cref="CombatAudio"/>: it never writes combat state, positions, health, timers,
    /// input or tuning, and never sets Time.timeScale. Hitstop and slow motion go through the Architect's match time hook
    /// (<see cref="CombatMatchRunner.RequestHitstop"/>, <see cref="CombatMatchRunner.RequestSlowMotion"/>), the punch-in
    /// through <see cref="MatchCamera.PunchIn(Transform,Vector2,float,float)"/>, and the shake moves only the child
    /// Main Camera's local position (ARCHITECTURE 3.1).
    ///
    /// Events from one combat tick are collected and resolved in LateUpdate (<see cref="ImpactClassifier"/>), so a
    /// knockout raised at the end of the tick turns the blow that caused it into the knockout blow. Runs after default
    /// LateUpdates so the flash overlay copies the sprite WrestlerView settled on this frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class CombatTheatrics : MonoBehaviour
    {
        private const float PixelsPerUnit = 60f;

        [Header("Materials (assigned in the prefab)")]
        [Tooltip("Lit sprite material (URP 2D Sprite-Lit-Default) for blood, stains and dust, so they take the golden-hour 2D lights like the dirt and the wrestlers.")]
        [SerializeField] private Material litMaterial;
        [Tooltip("Unlit sprite material (URP 2D Sprite-Unlit-Default) for the white impact stars, so they stay pure white and catch the bloom.")]
        [SerializeField] private Material unlitMaterial;
        [Tooltip("Solid-silhouette flash material (shader RuralRumble/Juice/SpriteFlash) for the red flash overlay.")]
        [SerializeField] private Material flashMaterial;

        [Header("Impacts (Starting points)")]
        [Tooltip("Hitstop, shake, blood, stars and dust for each kind of impact, plus the knockout extras.")]
        [SerializeField] private ImpactFeelTable feel = new ImpactFeelTable();

        [Header("Knockout slow motion (Starting points)")]
        [Tooltip("Combat speed during the knockout slow motion (0.25 = quarter speed).")]
        [Range(0.05f, 1f)] [SerializeField] private float koSlowMotionScale = 0.25f;
        [Tooltip("Real seconds of knockout slow motion. It runs at the same time as the KO hitstop. Keep under about 1.4 s: Victory loads 1.5 s real time after the knockout.")]
        [Range(0f, 1.4f)] [SerializeField] private float koSlowMotionSeconds = 1.0f;
        [Tooltip("Camera punch-in size on the knocked-out wrestler, as a fraction of the default view (0.6 = much closer). The rig clamps it to its own minimum.")]
        [Range(0.3f, 1f)] [SerializeField] private float koPunchInSize = 0.6f;
        [Tooltip("Real seconds the punch-in holds before easing back.")]
        [Range(0f, 2f)] [SerializeField] private float koPunchInHoldSeconds = 1.3f;
        [Tooltip("Punch-in centre relative to the knocked-out wrestler's feet (world units), so the body, not the feet, is centred.")]
        [SerializeField] private Vector2 koPunchInOffset = new Vector2(0f, 0.9f);

        [Header("Red flash (Starting points)")]
        [Tooltip("Flash colour. HDR: values above 1 glow through the bloom.")]
        [ColorUsage(false, true)] [SerializeField] private Color flashColor = new Color(1.5f, 0.1f, 0.08f, 1f);
        [Tooltip("Flash opacity at its peak (1 = solid red silhouette).")]
        [Range(0f, 1f)] [SerializeField] private float flashPeak = 0.85f;
        [Tooltip("Real seconds the flash holds at its peak.")]
        [Range(0f, 0.5f)] [SerializeField] private float flashHoldSeconds = 0.06f;
        [Tooltip("Real seconds the flash takes to fade out after the hold.")]
        [Range(0f, 1f)] [SerializeField] private float flashFadeSeconds = 0.16f;
        [Tooltip("Knockout flash: hold, real seconds.")]
        [Range(0f, 1f)] [SerializeField] private float koFlashHoldSeconds = 0.2f;
        [Tooltip("Knockout flash: fade, real seconds.")]
        [Range(0f, 2f)] [SerializeField] private float koFlashFadeSeconds = 0.6f;

        [Header("Blood (Starting points)")]
        [Tooltip("Drop colours, picked at random per drop (dark to bright reds).")]
        [SerializeField] private Color[] bloodPalette =
        {
            new Color(0.38f, 0.02f, 0.03f, 1f),
            new Color(0.58f, 0.03f, 0.04f, 1f),
            new Color(0.78f, 0.06f, 0.06f, 1f),
            new Color(0.95f, 0.16f, 0.12f, 1f),
        };
        [Tooltip("Smallest drop, art pixels.")]
        [Range(1, 12)] [SerializeField] private int dropMinPixels = 3;
        [Tooltip("Largest drop, art pixels.")]
        [Range(1, 16)] [SerializeField] private int dropMaxPixels = 6;
        [Tooltip("Gravity on the drops, world units per second squared.")]
        [Range(0f, 80f)] [SerializeField] private float bloodGravity = 24f;
        [Tooltip("Lowest launch angle above horizontal, degrees (negative = can spray slightly down).")]
        [Range(-45f, 45f)] [SerializeField] private float sprayMinAngle = -10f;
        [Tooltip("Highest launch angle above horizontal, degrees.")]
        [Range(0f, 90f)] [SerializeField] private float sprayMaxAngle = 70f;
        [Tooltip("Chance (0 to 1) a drop splashes back toward the attacker instead of away.")]
        [Range(0f, 0.5f)] [SerializeField] private float backSplashChance = 0.12f;
        [Tooltip("Drops land on the receiver's feet depth plus or minus this much (world units), so stains spread a little in depth.")]
        [Range(0f, 1f)] [SerializeField] private float landingDepthJitter = 0.35f;
        [Tooltip("A drop always falls at least this far (world units) before it lands, even from a lying body.")]
        [Range(0f, 1f)] [SerializeField] private float minimumFall = 0.2f;
        [Tooltip("Random spread of the drops' starting point around the contact point, world units.")]
        [Range(0f, 0.5f)] [SerializeField] private float sprayOriginSpread = 0.12f;
        [Tooltip("Drops in flight at most (oldest replaced).")]
        [Range(50, 2000)] [SerializeField] private int maxDropsInFlight = 600;

        [Header("Ground stains (Starting points)")]
        [Tooltip("Stains kept at full strength; past this the oldest fade out. D-046: about 80.")]
        [Range(0, 300)] [SerializeField] private int stainCap = 80;
        [Tooltip("Real seconds an old stain takes to fade once past the cap.")]
        [Range(0f, 10f)] [SerializeField] private float stainFadeSeconds = 2f;
        [Tooltip("Chance (0 to 1) a landed drop leaves a stain.")]
        [Range(0f, 1f)] [SerializeField] private float stainChance = 0.55f;
        [Tooltip("Stain width as a multiple of the drop size (stains splat flat on the dirt).")]
        [Range(0.5f, 5f)] [SerializeField] private float stainWidthScale = 2f;
        [Tooltip("Stain height as a multiple of the drop size.")]
        [Range(0.25f, 3f)] [SerializeField] private float stainHeightScale = 0.8f;
        [Tooltip("Stain brightness relative to the drop colour (soaked into the dirt).")]
        [Range(0.2f, 1f)] [SerializeField] private float stainDarken = 0.7f;
        [Tooltip("Stain opacity.")]
        [Range(0.1f, 1f)] [SerializeField] private float stainOpacity = 0.9f;

        [Header("Impact stars (Starting points)")]
        [Tooltip("Star colour (unlit; white catches the bloom).")]
        [SerializeField] private Color starColor = Color.white;
        [Tooltip("Seconds a star lasts (follows combat time when that option is on, so it holds through the hitstop freeze).")]
        [Range(0.03f, 0.6f)] [SerializeField] private float starLifetime = 0.16f;
        [Tooltip("Star size at the end of its life, as a multiple of its start size.")]
        [Range(0.2f, 3f)] [SerializeField] private float starEndScale = 1.4f;
        [Tooltip("How far extra stars scatter around the contact point, world units.")]
        [Range(0f, 1f)] [SerializeField] private float extraStarSpread = 0.35f;

        [Header("Dust (Starting points)")]
        [Tooltip("Dust colour (lit, so the golden-hour light tints it like the dirt).")]
        [SerializeField] private Color dustColor = new Color(0.8f, 0.68f, 0.52f, 0.6f);
        [Tooltip("Smallest puff at birth, art pixels.")]
        [Range(2, 40)] [SerializeField] private int dustMinPixels = 8;
        [Tooltip("Largest puff at birth, art pixels.")]
        [Range(2, 60)] [SerializeField] private int dustMaxPixels = 14;
        [Tooltip("Puff size at the end of its life, as a multiple of its start size.")]
        [Range(0.5f, 5f)] [SerializeField] private float dustEndScale = 2.2f;
        [Tooltip("Seconds a puff lasts.")]
        [Range(0.1f, 2f)] [SerializeField] private float dustLifetime = 0.55f;
        [Tooltip("Sideways speed of the puffs, world units per second.")]
        [Range(0f, 10f)] [SerializeField] private float dustSpeed = 2.6f;
        [Tooltip("Upward speed of the puffs, world units per second.")]
        [Range(0f, 5f)] [SerializeField] private float dustRise = 0.7f;
        [Tooltip("Fraction of the puffs' speed lost per second.")]
        [Range(0f, 10f)] [SerializeField] private float dustDrag = 3.5f;
        [Tooltip("Knockdown / knockout falls: combat seconds from the knockdown to the body hitting the dirt (the Fall clip reaches the downed sprite at about 0.35 s).")]
        [Range(0f, 1.5f)] [SerializeField] private float fallDustDelay = 0.35f;
        [Tooltip("Knockdown / knockout falls: dust puffs when the body lands.")]
        [Range(0, 30)] [SerializeField] private int fallDustPuffs = 10;
        [Tooltip("Knockdown / knockout falls: dust centre from the feet toward the fall side, world units (the middle of the lying body).")]
        [Range(0f, 3f)] [SerializeField] private float fallDustBodyOffset = 1.2f;

        [Header("Screen shake")]
        [Tooltip("How many times per second the shake picks a new direction.")]
        [Range(5f, 120f)] [SerializeField] private float shakeFrequency = 40f;
        [Tooltip("Move the shake in whole art pixels (retro look).")]
        [SerializeField] private bool shakeSnapToPixels = true;

        [Header("Time and look")]
        [Tooltip("Blood, stars and dust follow the combat clock: they hang in the hitstop freeze and drift in the knockout slow motion. Off = real time. The flash and the shake always use real time.")]
        [SerializeField] private bool effectsFollowCombatTime = true;
        [Tooltip("Draw drops, stars and dust on the art pixel grid.")]
        [SerializeField] private bool snapEffectsToPixels = true;

        [Header("Sorting (Default layer; wrestlers and props are order 0)")]
        [Tooltip("Ground stains: above the backdrop (-100), under the wrestlers, truck and pool (0).")]
        [SerializeField] private int stainSortingOrder = -50;
        [Tooltip("Dust puffs.")]
        [SerializeField] private int dustSortingOrder = 6;
        [Tooltip("Blood drops in flight (above the stars, so the spray reads over the impact flash).")]
        [SerializeField] private int bloodSortingOrder = 10;
        [Tooltip("Impact stars.")]
        [SerializeField] private int starSortingOrder = 8;
        [Tooltip("Drops, stars and dust of a wrestler standing behind the truck or pool: under the props (0), above the stains.")]
        [SerializeField] private int behindPropsSortingOrder = -40;
        [Tooltip("A wrestler counts as behind the truck or pool within this many world units past its sides.")]
        [Range(0f, 4f)] [SerializeField] private float behindPropsMargin = 1.5f;

        private struct FallDust
        {
            public WrestlerId Wrestler;
            public int Side;
            public float Due;
        }

        private CombatMatch match;
        private CombatEvents events;
        private CombatMatchRunner runner;
        private MatchCamera matchCamera;
        private Transform shakeTarget;
        private Vector3 shakeBase;
        private Vector2 shakeOffset;
        private float shakeRetarget;

        private readonly TickEvents tick = new TickEvents();
        private readonly List<Impact> impacts = new List<Impact>();
        private readonly float[] lastHealth = new float[2];
        private readonly WrestlerFlashOverlay[] flashes = new WrestlerFlashOverlay[2];
        private readonly Transform[] roots = new Transform[2];
        private readonly List<FallDust> fallDust = new List<FallDust>();
        private readonly List<FxParticle> landed = new List<FxParticle>();
        private readonly ScreenShake shake = new ScreenShake();

        private FxParticleList drops;
        private FxParticleList stars;
        private FxParticleList dust;
        private StainBook stains;
        private ParticleBatch dropBatch;
        private ParticleBatch starBatch;
        private ParticleBatch dustBatch;
        private ParticleBatch dropBackBatch;
        private ParticleBatch starBackBatch;
        private ParticleBatch dustBackBatch;
        private bool spawnBehind;
        private ParticleBatch stainBatch;
        private Texture2D squareTex;
        private Texture2D starTex;
        private Texture2D puffTex;
        private Material flashInstance;
        private float combatClock;
        private bool knockoutPlayed;

        /// <summary>For tests and verification: stains currently on the ground.</summary>
        public int StainCount => stains != null ? stains.Count : 0;
        /// <summary>For tests and verification: drops currently in flight.</summary>
        public int DropsInFlight => drops != null ? drops.Count : 0;

        private void Start()
        {
            MatchBootstrap bootstrap = MatchBootstrap.Current;
            match = bootstrap != null ? bootstrap.Match : null;
            if (match == null)
            {
                Debug.LogWarning("CombatTheatrics: no running match (MatchBootstrap.Current.Match); theatrics are off.", this);
                enabled = false;
                return;
            }
            runner = bootstrap.Runner;
            matchCamera = bootstrap.MatchCamera;
            roots[0] = bootstrap.PlayerRoot;
            roots[1] = bootstrap.CpuRoot;

            Camera cam = matchCamera != null ? matchCamera.TargetCamera : null;
            if (cam != null && cam.transform != matchCamera.transform)
            {
                shakeTarget = cam.transform;
                shakeBase = shakeTarget.localPosition;
            }
            else
            {
                Debug.LogWarning("CombatTheatrics: no child camera under the MatchCamera rig; screen shake is off (ARCHITECTURE 3.1).", this);
            }

            BuildEffects();

            lastHealth[0] = match.Player.Health;
            lastHealth[1] = match.Cpu.Health;

            events = match.Events;
            events.HitLanded += OnHitLanded;
            events.ObstacleHit += OnObstacleHit;
            events.KnockedDown += OnKnockedDown;
            events.StateChanged += OnStateChanged;
            events.HealthChanged += OnHealthChanged;
            events.MatchEnded += OnMatchEnded;
        }

        private void BuildEffects()
        {
            squareTex = PixelTextures.Square("JuiceSquare");
            starTex = PixelTextures.Star("JuiceStar");
            puffTex = PixelTextures.Puff("JuicePuff");

            drops = new FxParticleList(maxDropsInFlight);
            stars = new FxParticleList(64);
            dust = new FxParticleList(256);
            stains = new StainBook(stainCap, stainFadeSeconds);

            int stainCapacity = Mathf.Max(1, stainCap * 2);
            stainBatch = new ParticleBatch(transform, "BloodStains", litMaterial, squareTex, stainSortingOrder, stainCapacity);
            dustBatch = new ParticleBatch(transform, "Dust", litMaterial, puffTex, dustSortingOrder, 256);
            dropBatch = new ParticleBatch(transform, "BloodDrops", litMaterial, squareTex, bloodSortingOrder, maxDropsInFlight);
            starBatch = new ParticleBatch(transform, "ImpactStars", unlitMaterial, starTex, starSortingOrder, 64);
            dustBackBatch = new ParticleBatch(transform, "DustBehindProps", litMaterial, puffTex, behindPropsSortingOrder, 256);
            dropBackBatch = new ParticleBatch(transform, "BloodDropsBehindProps", litMaterial, squareTex, behindPropsSortingOrder + 2, maxDropsInFlight);
            starBackBatch = new ParticleBatch(transform, "ImpactStarsBehindProps", unlitMaterial, starTex, behindPropsSortingOrder + 1, 64);

            if (flashMaterial != null)
            {
                flashInstance = new Material(flashMaterial) { name = "JuiceFlash (runtime)", hideFlags = HideFlags.DontSave };
                flashInstance.SetColor("_FlashColor", flashColor);
            }
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer sr = roots[i] != null ? roots[i].GetComponentInChildren<SpriteRenderer>(true) : null;
                if (sr == null)
                    Debug.LogWarning($"CombatTheatrics: no SpriteRenderer under the {(WrestlerId)i} wrestler; its red flash is off.", this);
                flashes[i] = sr != null && flashInstance != null ? new WrestlerFlashOverlay(sr, flashInstance) : null;
            }
        }

        private void OnDestroy()
        {
            if (events != null)
            {
                events.HitLanded -= OnHitLanded;
                events.ObstacleHit -= OnObstacleHit;
                events.KnockedDown -= OnKnockedDown;
                events.StateChanged -= OnStateChanged;
                events.HealthChanged -= OnHealthChanged;
                events.MatchEnded -= OnMatchEnded;
                events = null;
            }
            if (shakeTarget != null)
                shakeTarget.localPosition = shakeBase;
            foreach (WrestlerFlashOverlay f in flashes)
                f?.Destroy();
            dropBatch?.Destroy();
            starBatch?.Destroy();
            dustBatch?.Destroy();
            stainBatch?.Destroy();
            dropBackBatch?.Destroy();
            starBackBatch?.Destroy();
            dustBackBatch?.Destroy();
            DestroySafe(flashInstance);
            DestroySafe(squareTex);
            DestroySafe(starTex);
            DestroySafe(puffTex);
        }

        private void OnDisable()
        {
            if (shakeTarget != null)
                shakeTarget.localPosition = shakeBase;
        }

        private static void DestroySafe(Object o)
        {
            if (o != null)
                Destroy(o);
        }

        // ------------------------------------------------------------------ event collection (inside the combat tick)

        private void OnHitLanded(HitInfo hit) => tick.Hits.Add(hit);

        private void OnObstacleHit(WrestlerId wrestler, Vector2 contact) =>
            tick.Obstacles.Add(new KeyValuePair<WrestlerId, Vector2>(wrestler, contact));

        private void OnKnockedDown(WrestlerId wrestler, float seconds) => tick.KnockedDown[(int)wrestler] = true;

        private void OnStateChanged(WrestlerId wrestler, CombatState from, CombatState to)
        {
            bool down = to == CombatState.KnockedDown || to == CombatState.KnockedOut;
            if (to == CombatState.KnockedOut)
                tick.KnockedOut[(int)wrestler] = true;
            if (down && from == CombatState.Running)
                tick.DownFromRunning[(int)wrestler] = true;
        }

        private void OnHealthChanged(WrestlerId wrestler, float health)
        {
            int i = (int)wrestler;
            if (health < lastHealth[i] - 1e-4f)
                tick.Damaged[i] = true;
            lastHealth[i] = health;
        }

        private void OnMatchEnded(MatchResult result)
        {
            tick.HasEnded = true;
            tick.Ended = result;
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            float realDt = Time.unscaledDeltaTime;
            float scale = runner != null ? runner.CombatTimeScale : 1f;
            combatClock += realDt * scale;
            float fxDt = effectsFollowCombatTime ? realDt * scale : realDt;

            if (!tick.IsEmpty)
            {
                ImpactClassifier.Resolve(tick, match.Player.Position, match.Cpu.Position, impacts);
                foreach (Impact impact in impacts)
                    Play(impact);
                RequestTime(impacts);
                tick.Clear();
            }

            UpdateFallDust();
            SimulateAndDraw(fxDt);

            foreach (WrestlerFlashOverlay f in flashes)
                f?.Update(realDt);

            UpdateShake(realDt);
        }

        private void Play(Impact impact)
        {
            ImpactFeel f = feel.Get(impact.Kind);
            int r = (int)impact.Receiver;
            IWrestlerView view = match.Get(impact.Receiver);
            int fallback = view.FallSide != 0 ? view.FallSide : -view.HorizontalFacing;
            int dir = ImpactClassifier.AwayFrom(view.Position.x, impact.SourceX, fallback);

            if (impact.Knockout)
                flashes[r]?.Trigger(flashPeak, koFlashHoldSeconds, koFlashFadeSeconds);
            else
                flashes[r]?.Trigger(flashPeak, flashHoldSeconds, flashFadeSeconds);

            Emit(impact, view, f, dir);
            if (impact.Knockout)
                Emit(impact, view, feel.knockout, dir);

            // Knockdown and knockout falls (D-041): dust when the body hits the dirt, in combat time.
            if ((tick.KnockedDown[r] || tick.KnockedOut[r]) && view.FallSide != 0 && fallDustPuffs > 0)
            {
                fallDust.RemoveAll(d => d.Wrestler == impact.Receiver);
                fallDust.Add(new FallDust { Wrestler = impact.Receiver, Side = view.FallSide, Due = combatClock + fallDustDelay });
            }
        }

        private void Emit(Impact impact, IWrestlerView view, ImpactFeel f, int dir)
        {
            spawnBehind = BloodSpray.IsBehindObstacle(view.Position, match.Arena.Obstacles, behindPropsMargin);
            Vector2 origin = Origin(impact, view, f.contactHeight);
            SprayBlood(origin, dir, f.bloodDrops, f.bloodSpeed, view.Position.y);
            SpawnStars(origin, f.stars, f.starPixels);
            float groundX = Mathf.Abs(impact.Contact.x - view.Position.x) < 0.01f ? origin.x : impact.Contact.x;
            SpawnDust(new Vector2(groundX, view.Position.y), f.dustPuffs);
            shake.Kick(f.shakeAmplitude, f.shakeSeconds);
        }

        /// <summary>Contact point lifted to <paramref name="height01"/> of the receiver's current sprite. A contact at the receiver's
        /// own feet (stomp, slam, run collision) uses the sprite's centre x, so a lying body bleeds from its middle.</summary>
        private Vector2 Origin(Impact impact, IWrestlerView view, float height01)
        {
            SpriteRenderer sr = flashes[(int)impact.Receiver]?.Source;
            if (sr == null)
                return impact.Contact + new Vector2(0f, 2f * height01);
            Bounds b = sr.bounds;
            float x = Mathf.Abs(impact.Contact.x - view.Position.x) < 0.01f ? b.center.x : impact.Contact.x;
            float y = b.min.y + b.size.y * Mathf.Clamp01(height01);
            return new Vector2(x, y);
        }

        private void RequestTime(List<Impact> list)
        {
            if (runner == null || knockoutPlayed)
                return;
            float hitstop = 0f;
            Impact? ko = null;
            foreach (Impact i in list)
            {
                hitstop = Mathf.Max(hitstop, feel.Get(i.Kind).hitstopSeconds);
                if (i.Knockout && ko == null)
                    ko = i;
            }

            if (ko == null)
            {
                if (hitstop > 0f)
                    runner.RequestHitstop(hitstop);
                return;
            }

            // Knockout: hitstop and slow motion run at the same time on the hook's real-time clock, so the whole effect
            // lasts max(hitstop, slow motion) real seconds, inside the 1.5 s before Victory loads.
            knockoutPlayed = true;
            runner.RequestHitstop(Mathf.Max(hitstop, feel.knockout.hitstopSeconds));
            if (koSlowMotionSeconds > 0f)
                runner.RequestSlowMotion(koSlowMotionScale, koSlowMotionSeconds);

            if (matchCamera != null && koPunchInHoldSeconds > 0f)
            {
                if (tick.HasEnded && tick.Ended.IsDraw)
                    matchCamera.PunchIn((match.Player.Position + match.Cpu.Position) * 0.5f + koPunchInOffset, koPunchInSize, koPunchInHoldSeconds);
                else
                {
                    Transform root = roots[(int)ko.Value.Receiver];
                    if (root != null)
                        matchCamera.PunchIn(root, koPunchInOffset, koPunchInSize, koPunchInHoldSeconds);
                }
            }
        }

        // ------------------------------------------------------------------ spawning

        private void SprayBlood(Vector2 origin, int dir, int count, float speed, float feetY)
        {
            if (count <= 0 || bloodPalette == null || bloodPalette.Length == 0)
                return;
            ArenaRect walkable = match.Arena.Bounds;
            int minPx = Mathf.Max(1, Mathf.Min(dropMinPixels, dropMaxPixels));
            int maxPx = Mathf.Max(minPx, dropMaxPixels);
            for (int i = 0; i < count; i++)
            {
                Vector2 start = origin + new Vector2(Random.Range(-sprayOriginSpread, sprayOriginSpread), Random.Range(-sprayOriginSpread, sprayOriginSpread));
                float size = Random.Range(minPx, maxPx + 1) / PixelsPerUnit;
                drops.Add(new FxParticle
                {
                    Position = start,
                    Velocity = BloodSpray.Velocity(dir, speed, sprayMinAngle, sprayMaxAngle, backSplashChance, Random.value, Random.value, Random.value),
                    Gravity = bloodGravity,
                    Drag = 0.15f,
                    Lands = true,
                    GroundY = BloodSpray.GroundY(feetY, Random.Range(-landingDepthJitter, landingDepthJitter), start.y, minimumFall, walkable),
                    Lifetime = 4f,
                    StartSize = size,
                    EndSize = size,
                    Color = bloodPalette[Random.Range(0, bloodPalette.Length)],
                    FadeFrom = 1f,
                    Behind = spawnBehind,
                });
            }
        }

        private void SpawnStars(Vector2 origin, int count, float pixels)
        {
            for (int i = 0; i < count; i++)
            {
                float px = i == 0 ? pixels : pixels * Random.Range(0.45f, 0.7f);
                Vector2 pos = i == 0 ? origin : origin + Random.insideUnitCircle * extraStarSpread;
                float size = px / PixelsPerUnit;
                stars.Add(new FxParticle
                {
                    Position = pos,
                    Lifetime = starLifetime * (i == 0 ? 1f : Random.Range(0.7f, 1f)),
                    StartSize = size,
                    EndSize = size * starEndScale,
                    Color = starColor,
                    FadeFrom = 0.4f,
                    Behind = spawnBehind,
                });
            }
        }

        private void SpawnDust(Vector2 ground, int count)
        {
            int minPx = Mathf.Max(1, Mathf.Min(dustMinPixels, dustMaxPixels));
            int maxPx = Mathf.Max(minPx, dustMaxPixels);
            for (int i = 0; i < count; i++)
            {
                float side = (i % 2 == 0 ? 1f : -1f) * Random.Range(0.3f, 1f);
                float size = Random.Range(minPx, maxPx + 1) / PixelsPerUnit;
                dust.Add(new FxParticle
                {
                    Position = ground + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0f, 0.12f)),
                    Velocity = new Vector2(side * dustSpeed, Random.Range(0.2f, 1f) * dustRise),
                    Drag = dustDrag,
                    Lifetime = dustLifetime * Random.Range(0.75f, 1.15f),
                    StartSize = size,
                    EndSize = size * dustEndScale,
                    Color = dustColor,
                    FadeFrom = 0.35f,
                    Behind = spawnBehind,
                });
            }
        }

        private void UpdateFallDust()
        {
            for (int i = fallDust.Count - 1; i >= 0; i--)
            {
                FallDust d = fallDust[i];
                if (combatClock < d.Due)
                    continue;
                fallDust.RemoveAt(i);
                Vector2 feet = match.Get(d.Wrestler).Position;
                spawnBehind = BloodSpray.IsBehindObstacle(feet, match.Arena.Obstacles, behindPropsMargin);
                SpawnDust(new Vector2(feet.x + d.Side * fallDustBodyOffset, feet.y), fallDustPuffs);
                shake.Kick(feel.knockdown.shakeAmplitude, feel.knockdown.shakeSeconds);
            }
        }

        // ------------------------------------------------------------------ simulation and drawing

        private void SimulateAndDraw(float fxDt)
        {
            float now = Time.unscaledTime;
            landed.Clear();
            drops.Step(fxDt, landed);
            stars.Step(fxDt, null);
            dust.Step(fxDt, null);

            stains.Cap = stainCap;
            stains.FadeSeconds = stainFadeSeconds;
            if (landed.Count > 0)
            {
                ArenaRect walkable = match.Arena.Bounds;
                foreach (FxParticle p in landed)
                {
                    if (Random.value >= stainChance)
                        continue;
                    if (!BloodSpray.TryPlaceStain(p.Position, walkable, match.Arena.Obstacles, out Vector2 at))
                        continue;
                    Color c = p.Color;
                    c = new Color(c.r * stainDarken, c.g * stainDarken, c.b * stainDarken, stainOpacity);
                    float s = p.StartSize;
                    stains.Add(new Stain { Position = at, Width = s * stainWidthScale, Height = s * stainHeightScale, Color = c }, now);
                }
            }
            stains.Update(now);

            float snap = snapEffectsToPixels ? PixelsPerUnit : 0f;

            stainBatch.Begin();
            for (int i = 0; i < stains.Count; i++)
            {
                Stain s = stains[i];
                Color32 c = s.Color;
                c.a = (byte)Mathf.RoundToInt(c.a * stains.Alpha01(i, now));
                stainBatch.Add(s.Position, s.Width, s.Height, c, snap);
            }
            stainBatch.End();

            DrawList(dropBatch, dropBackBatch, drops, snap);
            DrawList(starBatch, starBackBatch, stars, snap);
            DrawList(dustBatch, dustBackBatch, dust, snap);
        }

        private static void DrawList(ParticleBatch front, ParticleBatch behind, FxParticleList list, float snap)
        {
            front.Begin();
            behind.Begin();
            for (int i = 0; i < list.Count; i++)
            {
                FxParticle p = list[i];
                Color32 c = p.Color;
                c.a = (byte)Mathf.RoundToInt(c.a * p.Alpha01);
                float size = p.Size;
                (p.Behind ? behind : front).Add(p.Position, size, size, c, snap);
            }
            front.End();
            behind.End();
        }

        private void UpdateShake(float realDt)
        {
            if (shakeTarget == null)
                return;
            shake.Advance(realDt);
            float strength = shake.Strength;
            if (strength <= 0f)
            {
                shakeOffset = Vector2.zero;
                shakeTarget.localPosition = shakeBase;
                return;
            }
            shakeRetarget -= realDt;
            if (shakeRetarget <= 0f)
            {
                shakeRetarget = 1f / Mathf.Max(1f, shakeFrequency);
                shakeOffset = ScreenShake.Offset(1f, Random.value, Random.value);
            }
            Vector2 offset = shakeOffset * strength;
            if (shakeSnapToPixels)
                offset = ScreenShake.SnapToPixels(offset, PixelsPerUnit);
            shakeTarget.localPosition = shakeBase + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
