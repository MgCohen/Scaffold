using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    public class EntityDefinition : ScriptableObject
    {
        public IReadOnlyList<AttributeEntry> Entries => entries;
        [SerializeField] private List<AttributeEntry> entries = new List<AttributeEntry>();

        private readonly Dictionary<Attribute, AttributeValue> baseValues = new Dictionary<Attribute, AttributeValue>();

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.EnsureValueMatchesType();
            }

            RebuildLookup();
        }

        internal void RebuildLookup()
        {
            baseValues.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                AttributeEntry entry = entries[i];
                if (entry == null || entry.Attribute == null || entry.BaseValue == null)
                {
                    continue;
                }

                Attribute key = (Attribute)entry.Attribute;
                baseValues[key] = entry.BaseValue;
            }
        }

        public bool TryGetBaseValue(Attribute key, out AttributeValue value)
        {
            return baseValues.TryGetValue(key, out value);
        }

        internal void AddEntry(AttributeEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }
    }
}

