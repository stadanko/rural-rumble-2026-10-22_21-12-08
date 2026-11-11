using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Selectable wrestler (ARCHITECTURE 4.4). Holds no combat stats: both wrestlers share CombatTuning (D-005).
    /// </summary>
    [CreateAssetMenu(fileName = "Wrestler", menuName = "Rural Rumble/Wrestler Definition")]
    public sealed class WrestlerDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portrait;
        [Tooltip("Presentation-only wrestler prefab from Assets/Prefabs/Wrestlers/.")]
        [SerializeField] private GameObject prefab;

        [Header("Character Select")]
        [Tooltip("Full ring name shown on Character Select, e.g. Barry \"Hot Potato\" Lankford.")]
        [SerializeField] private string fullName;
        [Tooltip("One-line tagline shown under the name on Character Select.")]
        [SerializeField] private string tagline;
        [Tooltip("Profile image (trading card) shown on Character Select.")]
        [SerializeField] private Sprite selectCard;

        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public GameObject Prefab => prefab;
        /// <summary>Full ring name; falls back to <see cref="DisplayName"/> when empty.</summary>
        public string FullName => string.IsNullOrEmpty(fullName) ? displayName : fullName;
        public string Tagline => tagline;
        public Sprite SelectCard => selectCard;
    }
}
