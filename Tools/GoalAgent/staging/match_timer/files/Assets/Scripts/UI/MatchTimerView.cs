using RuralRumble.Combat;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>
    /// Match timer (top center). Display only: reads CombatMatch.ElapsedSeconds and never changes match state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchTimerView : MonoBehaviour
    {
        private Label timer;
        private CombatMatch match;
        private int shownSeconds = -1;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            timer = root.Q<Label>("timer");
            shownSeconds = -1;
        }

        // Start, not OnEnable: MatchBootstrap.Current.Match is valid only from default-order Start.
        private void Start()
        {
            MatchBootstrap bootstrap = MatchBootstrap.Current;
            if (bootstrap == null || bootstrap.Match == null)
            {
                Debug.LogError("MatchTimerView: no running match to show.", this);
                return;
            }
            match = bootstrap.Match;
            Refresh();
        }

        private void Update()
        {
            if (match != null) Refresh();
        }

        private void Refresh()
        {
            if (timer == null) return;
            int seconds = MatchTimerFormat.WholeSeconds(match.ElapsedSeconds);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            timer.text = MatchTimerFormat.Format(seconds);
        }
    }
}
