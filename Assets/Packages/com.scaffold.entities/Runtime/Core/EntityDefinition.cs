using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Shared definition (flyweight intrinsic state): default attributes keyed by <see cref="AttributeSO"/>.
    /// Modifiers do not live on definitions—only on <see cref="EntityInstanceState"/>.
    /// </summary>
    public class EntityDefinition : ScriptableObject
    {
        public IReadOnlyList<EntityDefinitionDefaultEntry> DefaultAttributes => defaultAttributes;

        [SerializeField]
        private List<EntityDefinitionDefaultEntry> defaultAttributes = new List<EntityDefinitionDefaultEntry>();

        private readonly Dictionary<AttributeSO, EntityDefinitionDefaultEntry> attributeToEntry =
            new Dictionary<AttributeSO, EntityDefinitionDefaultEntry>();

        private readonly Dictionary<string, AttributeSO> nameToAttribute =
            new Dictionary<string, AttributeSO>(StringComparer.Ordinal);

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            attributeToEntry.Clear();
            nameToAttribute.Clear();
            for (int i = 0; i < defaultAttributes.Count; i++)
            {
                EntityDefinitionDefaultEntry entry = defaultAttributes[i];
                if (entry?.Attribute == null)
                {
                    continue;
                }

                AttributeSO so = entry.Attribute;
                attributeToEntry[so] = entry;
                nameToAttribute[so.name] = so;
            }
        }

        public Attribute GetBaseAttribute(AttributeSO attribute)
        {
            if (attribute == null)
            {
                return default;
            }

            if (TryGetDefaultEntry(attribute, out EntityDefinitionDefaultEntry entry))
            {
                return entry.GetDefaultAttribute();
            }

            return (Attribute)attribute;
        }

        public bool TryGetAttributeSOByName(string assetName, out AttributeSO attribute)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                attribute = null;
                return false;
            }

            return nameToAttribute.TryGetValue(assetName, out attribute);
        }

        public bool TryGetDefaultEntry(AttributeSO attribute, out EntityDefinitionDefaultEntry entry)
        {
            if (attribute == null)
            {
                entry = null;
                return false;
            }

            return attributeToEntry.TryGetValue(attribute, out entry);
        }
    }
}
