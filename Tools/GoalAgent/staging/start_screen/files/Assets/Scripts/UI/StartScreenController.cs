using System;
using RuralRumble.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>Start screen: blinking prompt, any keyboard or gamepad button continues.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class StartScreenController : MonoBehaviour
    {
        const string HiddenClass = "prompt--hidden";
        const long BlinkMs = 600;

        [SerializeField] float inputDelaySeconds = 0.2f;

        IDisposable _anyButton;
        IVisualElementScheduledItem _blink;
        bool _loading;

        void OnEnable()
        {
            _loading = false;
            var root = GetComponent<UIDocument>().rootVisualElement;
            var prompt = root.Q<Label>("prompt");
            if (prompt != null)
            {
                _blink = prompt.schedule.Execute(() => prompt.ToggleInClassList(HiddenClass)).Every(BlinkMs);
            }

            // Small delay so the press that opened the scene doesn't skip it.
            _anyButton = InputSystem.onAnyButtonPress.Call(_ =>
            {
                if (_loading || Time.unscaledTime < inputDelaySeconds) return;
                _loading = true;
                SceneLoader.Load(StartScreenFlow.NextScene(IsInBuild));
            });
        }

        void OnDisable()
        {
            _anyButton?.Dispose();
            _anyButton = null;
            _blink?.Pause();
            _blink = null;
        }

        static bool IsInBuild(string sceneName)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return true;
            }
            return false;
        }
    }
}
