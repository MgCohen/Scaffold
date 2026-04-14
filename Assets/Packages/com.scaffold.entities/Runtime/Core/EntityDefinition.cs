using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    public class EntityDefinition : ScriptableObject
    {
        internal IReadOnlyList<VariableEntry> Entries => bag.Entries;

        [SerializeField] private VariableBag bag = new VariableBag();

        internal VariableBag Bag => bag;

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

        public bool TryGetBaseValue(Variable key, out VariableValue value)
        {
            return bag.TryGetBase(key, out value);
        }

        public void AddVariable(VariableSO variable, VariableValue defaultValue)
        {
            AddEntry(VariableEntry.Create(variable, defaultValue));
            RebuildLookup();
        }

        internal void AddEntry(VariableEntry entry)
        {
            if (entry != null)
            {
                bag.AddSerializedEntry(entry);
            }
        }

        internal void RebuildLookup()
        {
            bag.RebuildCache();
        }
    }
}
