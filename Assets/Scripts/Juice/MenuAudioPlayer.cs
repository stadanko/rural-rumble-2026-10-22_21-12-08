using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Plays menu sounds raised through <see cref="UiSoundEvents"/> (ARCHITECTURE 4.6). Created once at
    /// startup and kept with DontDestroyOnLoad, so a sound raised right before a scene load isn't cut off.
    /// It is the game's only persistent object and holds audio only. Clips and volumes come from
    /// <see cref="MenuAudioSettings"/> in a Resources folder; without that asset nothing is created and
    /// menus stay silent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuAudioPlayer : MonoBehaviour
    {
        private const int SourceCount = 3;

        private static MenuAudioPlayer instance;

        private MenuAudioSettings settings;
        private AudioSourcePool pool;

        // Domain reload is disabled in Play Mode options, so clear the static each Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            if (instance != null)
                return;
            var loaded = Resources.Load<MenuAudioSettings>(MenuAudioSettings.ResourcePath);
            if (loaded == null)
                return;

            var go = new GameObject("MenuAudioPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MenuAudioPlayer>();
            instance.settings = loaded;
        }

        private void Awake()
        {
            pool = new AudioSourcePool(gameObject, SourceCount);
        }

        private void OnEnable() => UiSoundEvents.Raised += Play;

        private void OnDisable() => UiSoundEvents.Raised -= Play;

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Play(UiSound sound)
        {
            if (settings == null || pool == null)
                return;
            pool.Play(settings.SlotFor(sound), 1f);
        }
    }
}
