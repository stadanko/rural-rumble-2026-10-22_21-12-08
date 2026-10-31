using System.Collections.Generic;
using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>All selectable wrestlers. Exactly two in the MVP (ARCHITECTURE 4.4).</summary>
    [CreateAssetMenu(fileName = "WrestlerRoster", menuName = "Rural Rumble/Wrestler Roster")]
    public sealed class WrestlerRoster : ScriptableObject
    {
        [SerializeField] private WrestlerDefinition[] wrestlers = new WrestlerDefinition[0];

        public IReadOnlyList<WrestlerDefinition> Wrestlers => wrestlers;
    }
}
