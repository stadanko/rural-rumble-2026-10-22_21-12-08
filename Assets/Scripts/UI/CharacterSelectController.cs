using RuralRumble.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RuralRumble.UI
{
    /// <summary>Character Select: Navigate picks left/right panel, Submit starts the match, Cancel goes back.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class CharacterSelectController : MonoBehaviour
    {
        const string SelectedClass = "panel--selected";

        [SerializeField] WrestlerRoster roster;
        [SerializeField] float inputDelaySeconds = 0.2f;

        readonly VisualElement[] _panels = new VisualElement[2];
        InputAction _navigate, _submit, _cancel;
        int _index;
        bool _loading;
        float _enabledAt;

        void OnEnable()
        {
            _loading = false;
            _index = 0;
            _enabledAt = Time.unscaledTime;

            var root = GetComponent<UIDocument>().rootVisualElement;
            for (int i = 0; i < 2; i++)
            {
                _panels[i] = root.Q<VisualElement>("panel-" + i);
                var def = roster != null && i < roster.Wrestlers.Count ? roster.Wrestlers[i] : null;
                var card = root.Q<VisualElement>("card-" + i);
                if (card != null && def != null && def.SelectCard != null)
                    card.style.backgroundImage = new StyleBackground(def.SelectCard);
                var name = root.Q<Label>("name-" + i);
                if (name != null) name.text = def != null ? def.FullName : string.Empty;
                var tagline = root.Q<Label>("tagline-" + i);
                if (tagline != null) tagline.text = def != null ? def.Tagline : string.Empty;
            }
            Refresh();

            var actions = InputSystem.actions;
            if (actions == null) return;
            var map = actions.FindActionMap("UI", false);
            if (map == null) return;
            if (!map.enabled) map.Enable();
            _navigate = map.FindAction("Navigate", false);
            _submit = map.FindAction("Submit", false);
            _cancel = map.FindAction("Cancel", false);
            if (_navigate != null) _navigate.performed += OnNavigate;
            if (_submit != null) _submit.performed += OnSubmit;
            if (_cancel != null) _cancel.performed += OnCancel;
        }

        void OnDisable()
        {
            if (_navigate != null) _navigate.performed -= OnNavigate;
            if (_submit != null) _submit.performed -= OnSubmit;
            if (_cancel != null) _cancel.performed -= OnCancel;
            _navigate = _submit = _cancel = null;
        }

        bool Ignoring => _loading || Time.unscaledTime - _enabledAt < inputDelaySeconds;

        void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (Ignoring || roster == null) return;
            int previous = _index;
            _index = CharacterSelectRules.Move(_index, ctx.ReadValue<Vector2>().x, roster.Wrestlers.Count);
            if (_index != previous) UiSoundEvents.Raise(UiSound.Navigate);
            Refresh();
        }

        void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (Ignoring || roster == null || roster.Wrestlers.Count < 2) return;
            _loading = true;
            GameSession.Setup = CharacterSelectRules.BuildSetup(roster.Wrestlers, _index);
            UiSoundEvents.Raise(UiSound.Confirm);
            SceneLoader.Load(SceneId.Match);
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (Ignoring) return;
            _loading = true;
            UiSoundEvents.Raise(UiSound.Back);
            SceneLoader.Load(SceneId.Start);
        }

        void Refresh()
        {
            for (int i = 0; i < _panels.Length; i++)
                _panels[i]?.EnableInClassList(SelectedClass, i == _index);
        }
    }
}
