using System;
using RuralRumble.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>Victory screen: shows the result, any keyboard or gamepad button returns to Start (D-026).</summary>
    [RequireComponent(typeof(UIDocument))]
    public class VictoryScreenController : MonoBehaviour
    {
        const string HintHiddenClass = "hint--hidden";
        const long BlinkMs = 600;

        [Tooltip("Used for preview when the scene is opened directly (entries 0 and 1).")]
        [SerializeField] WrestlerRoster roster;
        [SerializeField] float inputDelaySeconds = 1.0f;

        IDisposable _anyButton;
        IVisualElementScheduledItem _blink;
        bool _loading;
        float _enabledAt;

        void OnEnable()
        {
            _loading = false;
            _enabledAt = Time.unscaledTime;
            var root = GetComponent<UIDocument>().rootVisualElement;

            WrestlerDefinition player = null;
            WrestlerDefinition cpu = null;
            if (GameSession.HasSetup)
            {
                player = GameSession.Setup.Player;
                cpu = GameSession.Setup.Cpu;
            }
            else if (roster != null && roster.Wrestlers.Count >= 2)
            {
                player = roster.Wrestlers[0];
                cpu = roster.Wrestlers[1];
            }

            var content = VictoryText.Build(GameSession.LastResult, player, cpu, UnityEngine.Random.Range(0, int.MaxValue));
            Apply(root, content);

            var hint = root.Q<Label>("hint");
            if (hint != null)
            {
                _blink = hint.schedule.Execute(() => hint.ToggleInClassList(HintHiddenClass)).Every(BlinkMs);
            }

            _anyButton = InputSystem.onAnyButtonPress.Call(_ =>
            {
                if (_loading || Time.unscaledTime - _enabledAt < inputDelaySeconds) return;
                _loading = true;
                UiSoundEvents.Raise(UiSound.Confirm);
                SceneLoader.Load(SceneId.Start);
            });
        }

        void OnDisable()
        {
            _anyButton?.Dispose();
            _anyButton = null;
            _blink?.Pause();
            _blink = null;
        }

        static void Apply(VisualElement root, VictoryContent c)
        {
            SetCard(root.Q<VisualElement>("card-1"), c.FirstCard);
            SetCard(root.Q<VisualElement>("card-2"), c.SecondCard);

            var headline = root.Q<Label>("headline");
            if (headline != null) headline.text = c.Headline;

            var outcome = root.Q<Label>("outcome");
            if (outcome != null)
            {
                outcome.text = c.Outcome;
                outcome.RemoveFromClassList("outcome--win");
                outcome.RemoveFromClassList("outcome--lose");
                outcome.RemoveFromClassList("outcome--draw");
                switch (c.Kind)
                {
                    case VictoryOutcomeKind.Win: outcome.AddToClassList("outcome--win"); break;
                    case VictoryOutcomeKind.Lose: outcome.AddToClassList("outcome--lose"); break;
                    default: outcome.AddToClassList("outcome--draw"); break;
                }
            }

            var quote = root.Q<Label>("quote");
            if (quote != null)
            {
                bool has = !string.IsNullOrEmpty(c.Quote);
                quote.text = has ? c.Quote : "";
                quote.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static void SetCard(VisualElement card, WrestlerDefinition def)
        {
            if (card == null) return;
            if (def == null)
            {
                card.style.display = DisplayStyle.None;
                return;
            }
            card.style.display = DisplayStyle.Flex;
            if (def.SelectCard != null) card.style.backgroundImage = new StyleBackground(def.SelectCard);
        }
    }
}
