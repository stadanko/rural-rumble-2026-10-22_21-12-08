using System;
using RuralRumble.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>
    /// Start screen: the "RURAL RUMBLE" logo block falls in over the live yard, slams down (squash, bounce, jolt),
    /// raises <see cref="StartScreenEvents.LogoLanded"/> and <see cref="UiSound.LogoSlam"/> on impact, then shows the
    /// blinking prompt. A press during the intro skips to the landed state; the next press starts the game.
    /// Timing logic lives in <see cref="StartIntroSequence"/>; this class only applies it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StartScreenController : MonoBehaviour
    {
        const string HiddenClass = "prompt--hidden";
        const long BlinkMs = 600;
        // Off-screen fallback (panel px) until the block's layout is resolved; the reference height is 1080.
        const float FallbackOffscreen = 1600f;
        const float OffscreenMargin = 40f;
        // Caps one frame's step so a scene-load hitch doesn't swallow the fall.
        const float MaxStepSeconds = 0.05f;

        [SerializeField] float inputDelaySeconds = 0.2f;
        [SerializeField] StartIntroTimeline intro = new StartIntroTimeline();

        IDisposable _anyButton;
        IVisualElementScheduledItem _blink;
        VisualElement _titleBlock;
        Label _prompt;
        StartIntroSequence _sequence;

        void OnEnable()
        {
            _sequence = new StartIntroSequence(intro, inputDelaySeconds);
            var root = GetComponent<UIDocument>().rootVisualElement;
            _titleBlock = root.Q<VisualElement>("title-block");
            _prompt = root.Q<Label>("prompt");
            _prompt?.AddToClassList(HiddenClass);
            ApplyPose();

            // Input delay so the press that opened the scene doesn't skip or start it.
            _anyButton = InputSystem.onAnyButtonPress.Call(_ => Apply(_sequence.Press()));
        }

        void OnDisable()
        {
            _anyButton?.Dispose();
            _anyButton = null;
            _blink?.Pause();
            _blink = null;
        }

        void Update()
        {
            if (_sequence == null) return;
            Apply(_sequence.Advance(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds)));
            ApplyPose();
        }

        void Apply(StartIntroStep step)
        {
            if (step.RaiseLanded)
            {
                StartScreenEvents.RaiseLogoLanded(step.LandedSkipped);
                if (step.PlaySlam) UiSoundEvents.Raise(UiSound.LogoSlam);
            }
            if (step.ShowPrompt)
            {
                ApplyPose();
                ShowPrompt();
            }
            if (step.StartGame)
            {
                UiSoundEvents.Raise(UiSound.Start);
                SceneLoader.Load(StartScreenFlow.NextScene(IsInBuild));
            }
        }

        void ShowPrompt()
        {
            if (_prompt == null || _blink != null) return;
            _prompt.RemoveFromClassList(HiddenClass);
            var prompt = _prompt;
            _blink = prompt.schedule.Execute(() => prompt.ToggleInClassList(HiddenClass)).Every(BlinkMs).StartingIn(BlinkMs);
        }

        void ApplyPose()
        {
            if (_titleBlock == null || _sequence == null) return;
            var pose = _sequence.Pose;
            float y = -pose.OffsetFraction * OffscreenDistance() + pose.Shake.y;
            _titleBlock.style.translate = new Translate(pose.Shake.x, y);
            _titleBlock.style.scale = new Scale(new Vector3(pose.Scale.x, pose.Scale.y, 1f));
        }

        // Distance that puts the block's bottom edge above the top of the screen. Layout ignores translate, so
        // layout.yMax is the resting bottom edge in the root's coordinates.
        float OffscreenDistance()
        {
            var r = _titleBlock.layout;
            if (float.IsNaN(r.yMax) || r.height <= 0f) return FallbackOffscreen;
            return r.yMax + OffscreenMargin + intro.shakeAmplitude;
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
