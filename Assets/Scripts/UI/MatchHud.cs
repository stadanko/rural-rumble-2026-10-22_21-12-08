using RuralRumble.Combat;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>
    /// In-match HUD (Clean Retro, D-016). Display only: reads match state and never changes it.
    /// Top-middle is left free for the match timer.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchHud : MonoBehaviour
    {
        [Tooltip("Seconds the white trail waits after a hit before draining (unscaled time).")]
        [SerializeField] private float trailHoldSeconds = 0.4f;
        [Tooltip("Seconds the white trail takes to drain to the new health (unscaled time).")]
        [SerializeField] private float trailDrainSeconds = 0.5f;

        private sealed class Block
        {
            public Label Name;
            public VisualElement Fill;
            public VisualElement Trail;
            public VisualElement PortraitImage;
            public Label Initial;
            public readonly HealthTrail Math = new HealthTrail();
            public IWrestlerView View;
        }

        private readonly Block player = new Block();
        private readonly Block cpu = new Block();
        private CombatEvents events;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            Query(root, player, "player");
            Query(root, cpu, "cpu");
        }

        private static void Query(VisualElement root, Block block, string prefix)
        {
            block.Name = root.Q<Label>(prefix + "-name");
            block.Fill = root.Q<VisualElement>(prefix + "-fill");
            block.Trail = root.Q<VisualElement>(prefix + "-trail");
            block.PortraitImage = root.Q<VisualElement>(prefix + "-portrait");
            block.Initial = root.Q<Label>(prefix + "-initial");
        }

        // Start, not OnEnable: MatchBootstrap.Current.Match is valid only from default-order Start.
        private void Start()
        {
            MatchBootstrap bootstrap = MatchBootstrap.Current;
            if (bootstrap == null || bootstrap.Match == null)
            {
                Debug.LogError("MatchHud: no running match to show.", this);
                return;
            }

            CombatMatch match = bootstrap.Match;
            player.View = match.Player;
            cpu.View = match.Cpu;

            MatchSetup setup = bootstrap.Setup;
            Populate(player, setup.Player);
            Populate(cpu, setup.Cpu);

            player.Math.Reset(HealthTrail.ToFraction(player.View.Health, player.View.MaxHealth));
            cpu.Math.Reset(HealthTrail.ToFraction(cpu.View.Health, cpu.View.MaxHealth));
            Apply(player);
            Apply(cpu);

            events = match.Events;
            events.HealthChanged += OnHealthChanged;
        }

        private void OnDestroy()
        {
            if (events != null)
                events.HealthChanged -= OnHealthChanged;
            events = null;
        }

        private void Update()
        {
            if (player.View == null) return;
            Step(player);
            Step(cpu);
        }

        private void Step(Block block)
        {
            block.Math.Tick(Time.unscaledDeltaTime, trailDrainSeconds);
            Apply(block);
        }

        private void OnHealthChanged(WrestlerId id, float health)
        {
            Block block = id == WrestlerId.Player ? player : cpu;
            if (block.View == null) return;
            block.Math.SetFill(HealthTrail.ToFraction(health, block.View.MaxHealth), trailHoldSeconds);
            Apply(block);
        }

        private static void Apply(Block block)
        {
            if (block.Fill != null)
                block.Fill.style.width = Length.Percent(block.Math.Fill * 100f);
            if (block.Trail != null)
                block.Trail.style.width = Length.Percent(block.Math.Trail * 100f);
        }

        private static void Populate(Block block, WrestlerDefinition definition)
        {
            string displayName = definition != null ? definition.DisplayName : string.Empty;
            if (block.Name != null)
                block.Name.text = displayName.ToUpperInvariant();

            Sprite portrait = definition != null ? definition.Portrait : null;
            bool hasPortrait = portrait != null;
            if (block.PortraitImage != null)
            {
                block.PortraitImage.style.backgroundImage = hasPortrait
                    ? new StyleBackground(portrait)
                    : new StyleBackground(StyleKeyword.None);
                block.PortraitImage.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (block.Initial != null)
            {
                block.Initial.text = !hasPortrait && !string.IsNullOrEmpty(displayName)
                    ? displayName.Substring(0, 1).ToUpperInvariant()
                    : string.Empty;
                block.Initial.style.display = hasPortrait ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
    }
}
