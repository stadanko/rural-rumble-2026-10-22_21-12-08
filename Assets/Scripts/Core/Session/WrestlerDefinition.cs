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

        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public GameObject Prefab => prefab;
    }
}
