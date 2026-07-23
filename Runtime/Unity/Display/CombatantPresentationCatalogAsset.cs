using System;
using Combat.Unity.Authoring;
using UnityEngine;

namespace Combat.Unity.Display
{
    [Serializable]
    public struct CombatantPresentationCatalogEntry
    {
        [SerializeField] private CombatantConfigAsset _combatant;
        [SerializeField] private GameObject _prefab;

        public CombatantPresentationCatalogEntry(CombatantConfigAsset combatant, GameObject prefab)
        {
            _combatant = combatant;
            _prefab = prefab;
        }

        public CombatantConfigAsset Combatant => _combatant;
        public GameObject Prefab => _prefab;
        public string DefinitionId => _combatant != null ? _combatant.Id : string.Empty;
    }

    [CreateAssetMenu(menuName = "Combat/Display/Combatant Presentation Catalog", fileName = "CombatantPresentationCatalog")]
    public sealed class CombatantPresentationCatalogAsset : ScriptableObject
    {
        [SerializeField] private CombatantPresentationCatalogEntry[] _entries = Array.Empty<CombatantPresentationCatalogEntry>();

        public bool TryGetPrefab(string definitionId, out GameObject prefab)
        {
            if (!string.IsNullOrWhiteSpace(definitionId) && _entries != null)
            {
                for (var i = 0; i < _entries.Length; i++)
                {
                    CombatantPresentationCatalogEntry entry = _entries[i];
                    if (entry.Prefab != null && string.Equals(entry.DefinitionId, definitionId, StringComparison.Ordinal))
                    {
                        prefab = entry.Prefab;
                        return true;
                    }
                }
            }

            prefab = null;
            return false;
        }

        internal void ConfigureForTests(params CombatantPresentationCatalogEntry[] entries)
        {
            _entries = entries ?? Array.Empty<CombatantPresentationCatalogEntry>();
        }
    }
}
