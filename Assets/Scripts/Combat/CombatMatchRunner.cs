using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Combat
{
    /// <summary>
    /// Scene glue for <see cref="CombatMatch"/> (ARCHITECTURE 4.2 Tick, 4.5). Builds the match at Start
    /// from the inspector references (or from <see cref="Configure"/> if called first), ticks it every
    /// Update with Time.deltaTime, and writes each wrestler root's transform.position from
    /// IWrestlerView.Position. Holds no rules.
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

        public CombatMatch Match { get; private set; }
        public Transform PlayerRoot => playerRoot;
        public Transform CpuRoot => cpuRoot;

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
        }

        private void Update()
        {
            if (Match == null)
                return;
            Match.Tick(Time.deltaTime, playerSource.Read(), cpuSource.Read());
            WritePositions();
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
