using System.Collections;
using RuralRumble.Core;
using RuralRumble.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuralRumble.UI
{
    /// <summary>
    /// Match-end handoff (ARCHITECTURE 4.3, D-026). On MatchEnded it stores the result in
    /// <see cref="GameSession.LastResult"/>, waits the knockout hold in unscaled time, then loads the next
    /// scene once. Never touches combat state or Time.timeScale.
    /// </summary>
    public sealed class MatchFlow : MonoBehaviour
    {
        [Tooltip("Knockout hold in seconds (unscaled) before the next scene loads (D-026: about 1.5).")]
        [SerializeField] private float knockoutHoldSeconds = 1.5f;

        private Combat.CombatEvents events;
        private bool handled;

        // Start, not OnEnable: MatchBootstrap.Current.Match is valid only from default-order Start.
        private void Start()
        {
            MatchBootstrap bootstrap = MatchBootstrap.Current;
            if (bootstrap == null || bootstrap.Match == null)
            {
                Debug.LogError("MatchFlow: no running match to listen to.", this);
                return;
            }

            events = bootstrap.Match.Events;
            events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (events != null)
                events.MatchEnded -= OnMatchEnded;
            events = null;
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (handled) return;
            handled = true;
            GameSession.LastResult = result;
            StartCoroutine(LoadAfterHold());
        }

        private IEnumerator LoadAfterHold()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, knockoutHoldSeconds));
            SceneLoader.Load(MatchFlowRules.NextScene(IsSceneInBuild));
        }

        private static bool IsSceneInBuild(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return true;
            }
            return false;
        }
    }
}
