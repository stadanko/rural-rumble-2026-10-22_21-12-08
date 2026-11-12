using RuralRumble.AI;
using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Presentation;
using UnityEngine;

namespace RuralRumble.Match
{
    /// <summary>
    /// Match scene entry point (ARCHITECTURE 4.4, D-022). Holds no rules.
    ///
    /// Order:
    /// - Awake (any order vs. other Awakes): reads GameSession (fallback: roster entries 0 vs 1),
    ///   instantiates both wrestler prefabs at their spawn feet points, adds the player's
    ///   PlayerInputSource, and calls CombatMatchRunner.Configure with a CPU input relay. All Awakes run before any Start,
    ///   so the runner sees the spawned roots when it builds the CombatMatch in its Start.
    /// - CombatMatchRunner.Start (execution order -50): builds the CombatMatch.
    /// - Start (this, execution order -40, i.e. after the runner): binds both WrestlerViews to the
    ///   match's views and events, creates the CPU's OpponentBrain from the match's views and tuning and
    ///   points the relay at it, and hands the two roots to MatchCamera. Default-order (0) components such as
    ///   the HUD can read <see cref="Match"/> from their own Start onward.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        public static MatchBootstrap Current { get; private set; }

        [SerializeField] private WrestlerRoster roster;
        [SerializeField] private CombatMatchRunner combatRunner;
        [SerializeField] private MatchCamera matchCamera;

        [Tooltip("Match opened directly (no GameSession): play roster entry 1 vs 0 instead of 0 vs 1. Testing aid only.")]
        [SerializeField] private bool directPlaySwapWrestlers;

        [Tooltip("Testing aid: the CPU opponent just stands there. It doesn't move, attack, grapple, or mash to get up, " +
                 "but combat still applies to it, so the player can hit, slam, knock down and knock it out. " +
                 "Can be switched on or off during Play Mode. Turn it off for real matches.")]
        [SerializeField] private bool disableCpuAi;

        [Header("Spawn feet points (world units)")]
        [SerializeField] private Vector2 playerSpawn = new Vector2(9.0f, -11.6667f);
        [SerializeField] private Vector2 cpuSpawn = new Vector2(17.6667f, -11.6667f);

        private Transform playerRoot;
        private Transform cpuRoot;
        private WrestlerView playerView;
        private WrestlerView cpuView;
        private readonly InputRelay cpuInput = new InputRelay();

        /// <summary>Testing aid (Inspector "Disable Cpu Ai"): while true the CPU receives no input.</summary>
        public bool CpuAiDisabled
        {
            get => disableCpuAi;
            set => disableCpuAi = value;
        }

        /// <summary>The running combat match, or null before this component's Start.</summary>
        public CombatMatch Match => combatRunner != null ? combatRunner.Match : null;
        public MatchSetup Setup { get; private set; }
        public Transform PlayerRoot => playerRoot;
        public Transform CpuRoot => cpuRoot;
        /// <summary>The CPU's brain (ARCHITECTURE 4.1), or null before Start.</summary>
        public OpponentBrain CpuBrain { get; private set; }

        private void Awake()
        {
            Current = this;
            cpuInput.Muted = () => disableCpuAi;

            if (combatRunner == null)
            {
                Debug.LogError("MatchBootstrap: no CombatMatchRunner assigned.", this);
                return;
            }

            if (!TryResolveSetup(out MatchSetup setup))
                return;
            Setup = setup;

            playerRoot = Spawn(setup.Player, playerSpawn, "Player", out playerView);
            cpuRoot = Spawn(setup.Cpu, cpuSpawn, "Cpu", out cpuView);
            if (playerRoot == null || cpuRoot == null)
                return;

            var playerInput = gameObject.AddComponent<PlayerInputSource>();
            // The brain needs the match's views and tuning, which exist only after the runner's Start, so
            // the runner gets a relay now and the relay gets the brain in Start (ARCHITECTURE 4.4).
            // The definitions let Combat pick each initiator's bodyslam startup (D-033).
            combatRunner.Configure(playerRoot, cpuRoot, playerInput, cpuInput, setup.Player, setup.Cpu);
        }

        private void Start()
        {
            CombatMatch match = Match;
            if (match == null)
            {
                Debug.LogError("MatchBootstrap: CombatMatchRunner did not build a match.", this);
                return;
            }

            if (playerView != null)
            {
                playerView.Bind(match.Player);
                playerView.BindEvents(match.Events, WrestlerId.Player);
            }
            if (cpuView != null)
            {
                cpuView.Bind(match.Cpu);
                cpuView.BindEvents(match.Events, WrestlerId.Cpu);
            }

            // The CPU always drives the non-player wrestler through the same rules as the player (D-021).
            CpuBrain = new OpponentBrain(match.Cpu, match.Player, match.Tuning, () => match.ElapsedSeconds);
            cpuInput.Source = CpuBrain;

            if (matchCamera != null)
                matchCamera.SetTargets(playerRoot, cpuRoot);
        }

        private bool TryResolveSetup(out MatchSetup setup)
        {
            if (GameSession.HasSetup)
            {
                setup = GameSession.Setup;
                return true;
            }

            setup = default;
            if (roster == null || roster.Wrestlers.Count < 2)
            {
                Debug.LogError("MatchBootstrap: no GameSession setup and the roster has fewer than two wrestlers.", this);
                return false;
            }
            int player = directPlaySwapWrestlers ? 1 : 0;
            Debug.Log($"MatchBootstrap: no GameSession setup (Match opened directly); using roster entries {player} vs {1 - player}.", this);
            setup = new MatchSetup(roster.Wrestlers[player], roster.Wrestlers[1 - player]);
            return true;
        }

        private Transform Spawn(WrestlerDefinition definition, Vector2 feet, string role, out WrestlerView view)
        {
            view = null;
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogError($"MatchBootstrap: {role} wrestler definition or its prefab is missing.", this);
                return null;
            }

            GameObject instance = Instantiate(definition.Prefab, new Vector3(feet.x, feet.y, 0f), Quaternion.identity);
            instance.name = $"{role} ({definition.DisplayName})";
            view = instance.GetComponent<WrestlerView>();
            if (view == null)
                Debug.LogError($"MatchBootstrap: prefab {definition.Prefab.name} has no WrestlerView.", instance);
            return instance.transform;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }
    }

    /// <summary>
    /// Forwards Read() to a source set later (MatchBootstrap.Start). Idle until then; no combat tick
    /// happens before Start, so the idle reads are never used in practice.
    /// </summary>
    internal sealed class InputRelay : IWrestlerInput
    {
        public IWrestlerInput Source { get; set; }

        /// <summary>When it returns true the relay sends no input (MatchBootstrap's "Disable Cpu Ai" testing aid).
        /// The source isn't read at all, so the CPU brain simply pauses and resumes when re-enabled.</summary>
        public System.Func<bool> Muted { get; set; }

        public WrestlerIntent Read() =>
            Source == null || (Muted != null && Muted()) ? WrestlerIntent.None : Source.Read();
    }
}
