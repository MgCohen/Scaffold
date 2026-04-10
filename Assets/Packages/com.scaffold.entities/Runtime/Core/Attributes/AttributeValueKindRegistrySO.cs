using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(
        menuName = "Scaffold/Entity/Attribute Value Kind Registry",
        fileName = "AttributeValueKindRegistry")]
    public sealed class AttributeValueKindRegistrySO : ScriptableObject
    {
        public IReadOnlyList<AttributeDefinitionBase> Kinds => kinds;

        [SerializeReference]
        [SerializeField]
        private List<AttributeDefinitionBase> kinds = new List<AttributeDefinitionBase>();

        public bool TryGetByStableId(string stableId, out AttributeDefinitionBase definition)
        {
            definition = null!;
            if (string.IsNullOrEmpty(stableId))
            {
                return false;
            }

            EnsureCache();
            return stableIdToKind.TryGetValue(stableId, out definition!);
        }

        public bool TryGetFirstForLegacyType(AttributeValueType legacyType, out AttributeDefinitionBase definition)
        {
            definition = null!;
            if (legacyType == AttributeValueType.Custom)
            {
                return false;
            }

            EnsureCache();
            for (int i = 0; i < kinds.Count; i++)
            {
                AttributeDefinitionBase k = kinds[i];
                if (k == null)
                {
                    continue;
                }

                if (k.MapsToLegacyValueType == legacyType)
                {
                    definition = k;
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            InvalidateCache();
        }

        private void OnValidate()
        {
            InvalidateCache();
        }

        private void EnsureCache()
        {
            if (stableIdToKind != null)
            {
                return;
            }

            stableIdToKind = new Dictionary<string, AttributeDefinitionBase>(StringComparer.Ordinal);
            for (int i = 0; i < kinds.Count; i++)
            {
                AttributeDefinitionBase k = kinds[i];
                if (k == null)
                {
                    continue;
                }

                string id = k.StableTypeId;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!stableIdToKind.ContainsKey(id))
                {
                    stableIdToKind[id] = k;
                }
            }
        }

        private void InvalidateCache()
        {
            stableIdToKind = null;
        }

        internal void SetKindsForTests(IEnumerable<AttributeDefinitionBase> items)
        {
            kinds.Clear();
            foreach (AttributeDefinitionBase item in items)
            {
                if (item != null)
                {
                    kinds.Add(item);
                }
            }

            InvalidateCache();
        }

        [NonSerialized]
        private Dictionary<string, AttributeDefinitionBase> stableIdToKind;
    }
}
