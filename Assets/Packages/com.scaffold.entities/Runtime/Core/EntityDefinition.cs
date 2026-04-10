using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    public class EntityDefinition : ScriptableObject
    {
        public IReadOnlyList<AttributeEntry> Entries => bag.Entries;

        [SerializeField] private AttributeBag bag = new AttributeBag();

        internal AttributeBag Bag => bag;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            for (int i = 0; i < bag.Entries.Count; i++)
            {
                bag.Entries[i]?.EnsureValueMatchesType();
            }

            RebuildLookup();
        }

        internal void RebuildLookup()
        {
            bag.RebuildCache();
        }

        public bool TryGetBaseValue(Attribute key, out AttributeValue value)
        {
            return bag.TryGetBase(key, out value);
        }

        internal void AddEntry(AttributeEntry entry)
        {
            if (entry != null)
            {
                bag.AddSerializedEntry(entry);
            }
        }
    }
}
