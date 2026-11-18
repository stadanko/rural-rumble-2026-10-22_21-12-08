using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Scene glue for <see cref="CombatMatch"/> (ARCHITECTURE 4.2 Tick, 4.5). Builds the match at Start
    /// from the inspector references (or from <see cref="Configure"/> if called first), ticks it every
    /// Update with Time.deltaTime, and writes each wrestler root's transform.position from
    /// IWrestlerView.Position. Holds no rules.
    ///
    /// Match time hook (D-046, ARCHITECTURE 4.2): <see cref="RequestHitstop"/> and <see cref="RequestSlowMotion"/>
    /// let presentation (Juice) pause or slow the combat tick and the wrestlers' Animators without touching
    /// Time.timeScale. The frame's real seconds go through a <see cref="MatchClock"/>; Tick gets the combat seconds.
    /// A fully frozen frame skips Tick and the input reads (presses stay buffered in the input sources). With no
    /// request running the clock is idle and the runner behaves exactly as before (Tick(Time.deltaTime), Animators
    /// untouched).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class CombatMatchRunner : MonoBehaviour
    {
        [Tooltip("The shared tuning asset (Assets/Data/Tuning/CombatTuning.asset).")]
        [SerializeField] private CombatTuning tuning;

        [Header("Wrestler roots (start positions are read from them at Start)")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform cpuRoot;

        [Header("Input")]
        [Tooltip("Component implementing IWrestlerInput for the player (PlayerInputSource).")]
        [SerializeField] private MonoBehaviour playerInput;
        [Tooltip("Optional component implementing IWrestlerInput for the CPU. Empty = idle stand-in.")]
        [SerializeField] private MonoBehaviour cpuInput;

        [Header("Arena")]
        [Tooltip("Root whose children hold ArenaObstacleMarkers (the Arena prefab instance). Empty = search the scene.")]
        [SerializeField] private Transform arenaRoot;

        private IWrestlerInput playerSource;
        private IWrestlerInput cpuSource;
        private WrestlerDefinition playerWrestler;
        private WrestlerDefinition cpuWrestler;
        private readonly MatchClock clock = new MatchClock();
        private Animator[] wrestlerAnimators = System.Array.Empty<Animator>();
        private float appliedAnimatorSpeed = 1f;

        public CombatMatch Match { get; private set; }
        public Transform PlayerRoot => playerRoot;
        public Transform CpuRoot => cpuRoot;

        /// <summary>The time hook's state (read-only use; request through the methods below).</summary>
        public MatchClock Clock => clock;

        /// <summary>Combat time scale right now: 0 during a hitstop, the slow-motion scale, else 1.</summary>
        public float CombatTimeScale => clock.CurrentScale;

        /// <summary>
        /// D-046 hitstop: freeze the combat tick (timers, movement, the match timer) and both wrestlers' Animators
        /// for <paramref name="realSeconds"/> of real (unscaled) time. Overlapping requests keep the longer remaining
        /// time; they never add up. Ignored if not positive.
        /// </summary>
        public void RequestHitstop(float realSeconds) => clock.RequestHitstop(realSeconds);

        /// <summary>
        /// D-046 slow motion: run the combat tick and both wrestlers' Animators at <paramref name="scale"/> (0..1) for
        /// <paramref name="realSeconds"/> of real time. Replaces any slow motion already running. A hitstop during it
        /// still freezes; both durations count real time.
        /// </summary>
        public void RequestSlowMotion(float scale, float realSeconds) => clock.RequestSlowMotion(scale, realSeconds);

        /// <summary>Ends any hitstop and slow motion now (the next frame runs at normal speed).</summary>
        public void CancelTimeEffects() => clock.Cancel();

        /// <summary>
        /// Optional runtime setup (for MatchBootstrap once it spawns wrestlers). Call before Start;
        /// any argument left null keeps the inspector value. <paramref name="playerDefinition"/> /
        /// <paramref name="cpuDefinition"/> say which wrestler each side plays; Combat uses them only for
        /// per-wrestler tuning entries (bodyslam Startup per initiator, D-033). Null = the shared defaults.
        /// </summary>
        public void Configure(Transform player, Transform cpu, IWrestlerInput playerInputSource, IWrestlerInput cpuInputSource,
            WrestlerDefinition playerDefinition = null, WrestlerDefinition cpuDefinition = null)
        {
            if (playerDefinition != null) playerWrestler = playerDefinition;
            if (cpuDefinition != null) cpuWrestler = cpuDefinition;
            if (player != null) playerRoot = player;
            if (cpu != null) cpuRoot = cpu;
            if (playerInputSource != null) playerSource = playerInputSource;
            if (cpuInputSource != null) cpuSource = cpuInputSource;
        }

        private void Start()
        {
            if (tuning == null)
            {
                Debug.LogError("CombatMatchRunner: no CombatTuning assigned.", this);
                enabled = false;
                return;
            }
            if (playerRoot == null || cpuRoot == null)
            {
                Debug.LogError("CombatMatchRunner: player and CPU roots must be assigned.", this);
                enabled = false;
                return;
            }

            playerSource ??= playerInput as IWrestlerInput;
            cpuSource ??= cpuInput as IWrestlerInput;
            if (playerSource == null)
            {
                Debug.LogWarning("CombatMatchRunner: player input is missing or does not implement IWrestlerInput; player is idle.", this);
                playerSource = IdleInput.Instance;
            }
            cpuSource ??= IdleInput.Instance;

            ArenaObstacleMarker[] markers = arenaRoot != null
                ? arenaRoot.GetComponentsInChildren<ArenaObstacleMarker>()
                : FindObjectsByType<ArenaObstacleMarker>(FindObjectsSortMode.None);
            ArenaGeometry arena = ArenaObstacleMarker.BuildGeometry(markers);

            Match = new CombatMatch(tuning, arena, playerRoot.position, cpuRoot.position,
                playerWrestler: playerWrestler, cpuWrestler: cpuWrestler);
            WritePositions();

            var animators = new System.Collections.Generic.List<Animator>();
            animators.AddRange(playerRoot.GetComponentsInChildren<Animator>(true));
            animators.AddRange(cpuRoot.GetComponentsInChildren<Animator>(true));
            wrestlerAnimators = animators.ToArray();
        }

        private void Update()
        {
            if (Match == null)
                return;

            float realDt = Time.deltaTime;
            bool hookActive = !clock.IsIdle;
            float combatDt = clock.Advance(realDt);
            if (hookActive || appliedAnimatorSpeed != 1f)
                SetAnimatorSpeed(realDt > 0f ? combatDt / realDt : clock.CurrentScale);

            // Fully frozen by the hook: no tick and no input reads, so presses wait for the thaw.
            if (hookActive && realDt > 0f && combatDt <= 0f)
                return;

            Match.Tick(combatDt, playerSource.Read(), cpuSource.Read());
            WritePositions();
        }

        // Animator.speed is not used by presentation (WrestlerView drives move speed through its MoveSpeed parameter),
        // so the hook owns it: 1 whenever the hook is idle.
        private void SetAnimatorSpeed(float speed)
        {
            appliedAnimatorSpeed = speed;
            foreach (Animator animator in wrestlerAnimators)
                if (animator != null)
                    animator.speed = speed;
        }

        private void WritePositions()
        {
            Write(playerRoot, Match.Player.Position);
            Write(cpuRoot, Match.Cpu.Position);
        }

        private static void Write(Transform root, Vector2 feet)
        {
            root.position = new Vector3(feet.x, feet.y, root.position.z);
        }

        private void OnValidate()
        {
            if (playerInput != null && !(playerInput is IWrestlerInput))
            {
                Debug.LogWarning($"CombatMatchRunner: {playerInput.GetType().Name} does not implement IWrestlerInput.", this);
                playerInput = null;
            }
            if (cpuInput != null && !(cpuInput is IWrestlerInput))
            {
                Debug.LogWarning($"CombatMatchRunner: {cpuInput.GetType().Name} does not implement IWrestlerInput.", this);
                cpuInput = null;
            }
        }
    }
}
