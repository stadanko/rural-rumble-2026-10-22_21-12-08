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
    ///   PlayerInputSource, and calls CombatMatchRunner.Configure. All Awakes run before any Start,
    ///   so the runner sees the spawned roots when it builds the CombatMatch in its Start.
    /// - CombatMatchRunner.Start (execution order -50): builds the CombatMatch.
    /// - Start (this, execution order -40, i.e. after the runner): binds both WrestlerViews to the
    ///   match's views and hands the two roots to MatchCamera. Default-order (0) components such as
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

        [Header("Spawn feet points (world units)")]
        [SerializeField] private Vector2 playerSpawn = new Vector2(9.0f, -11.6667f);
        [SerializeField] private Vector2 cpuSpawn = new Vector2(17.6667f, -11.6667f);

        private Transform playerRoot;
        private Transform cpuRoot;
        private WrestlerView playerView;
        private WrestlerView cpuView;

        /// <summary>The running combat match, or null before this component's Start.</summary>
        public CombatMatch Match => combatRunner != null ? combatRunner.Match : null;
        public MatchSetup Setup { get; private set; }
        public Transform PlayerRoot => playerRoot;
        public Transform CpuRoot => cpuRoot;

        private void Awake()
        {
            Current = this;

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
            // CPU input left null: idle stand-in until OpponentBrain exists.
            combatRunner.Configure(playerRoot, cpuRoot, playerInput, null);
        }

        private void Start()
        {
            CombatMatch match = Match;
            if (match == null)
            {
                Debug.LogError("MatchBootstrap: CombatMatchRunner did not build a match.", this);
                return;
            }

            if (playerView != null) playerView.Bind(match.Player);
            if (cpuView != null) cpuView.Bind(match.Cpu);

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
            Debug.Log("MatchBootstrap: no GameSession setup (Match opened directly); using roster entries 0 vs 1.", this);
            setup = new MatchSetup(roster.Wrestlers[0], roster.Wrestlers[1]);
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
}
