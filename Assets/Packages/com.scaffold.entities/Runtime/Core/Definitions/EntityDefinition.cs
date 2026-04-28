using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public class EntityDefinition : IEntityDefinition, IDefinitionVariableBagProvider
    {
        public IEnumerable<Variable> DefinedVariables => bag.LocalKeys;

        internal IReadOnlyList<VariableEntry> Entries => bag.Entries;

        internal VariableBag Bag => bag;

        [SerializeField] private VariableBag bag = new VariableBag();

        VariableBag IDefinitionVariableBagProvider.Bag => Bag;

        public bool TryGetDefaultValue(Variable key, out VariableValue value)
        {
            return bag.TryGetBase(key, out value);
        }

        public void AddVariable(Variable key, VariableValue defaultValue)
        {
            AddEntry(VariableEntry.Create(key, defaultValue));
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

        void IDefinitionVariableBagProvider.RebuildLookup()
        {
            RebuildLookup();
        }
    }
}
