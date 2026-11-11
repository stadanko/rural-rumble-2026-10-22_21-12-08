using UnityEditor;
using UnityEditor.SceneManagement;

namespace RuralRumble.EditorBuild
{
    /// <summary>
    /// Makes the Editor's Play button start the game loop on the first Build Settings scene (Start),
    /// whatever scene is open for editing, like a real build does. Editor only; builds are unaffected.
    /// Toggle with Rural Rumble &gt; Play From Start Screen (on by default) to play the open scene
    /// directly instead, e.g. Match.unity for combat testing.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromStartScene
    {
        private const string MenuPath = "Rural Rumble/Play From Start Screen";
        private const string PrefKey = "RuralRumble.PlayFromStartScene";

        static PlayFromStartScene()
        {
            Apply();
            // Build Settings may not be loaded yet during the very first editor load; apply again on the next tick
            // and right before entering Play Mode, so the setting is always current (e.g. after Build Settings edits).
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Apply();
            };
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void Apply()
        {
            SceneAsset start = null;
            if (Enabled)
            {
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled) continue;
                    start = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                    break;
                }
            }
            EditorSceneManager.playModeStartScene = start;
        }
    }
}
