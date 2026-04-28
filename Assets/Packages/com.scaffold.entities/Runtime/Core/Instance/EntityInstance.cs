using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public partial class EntityInstance<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition> where TDefinition : IEntityDefinition
    {
        [SerializeField] private LocalVariableStorage localStorage = new LocalVariableStorage();

        public void Initialize(InstanceId instanceId, TDefinition entityDefinition)
        {
            if (entityDefinition == null)
            {
                throw new ArgumentNullException(nameof(entityDefinition));
            }

            if (entityDefinition is IDefinitionVariableBagProvider bagSource)
            {
                bagSource.RebuildLookup();
            }

            localStorage.WireToDefinition(entityDefinition);
            Initialize(instanceId, entityDefinition, localStorage);
        }

        public ModifierId AddModifier(EntityModifierEntry entry)
        {
            return localStorage.AddModifier(entry);
        }

        public bool RemoveModifier(Variable key, ModifierId id)
        {
            return localStorage.RemoveModifier(key, id);
        }

        public void ClearModifiers()
        {
            localStorage.ClearModifiers();
        }

        public bool AddVariable(Variable key, VariableValue initialBase)
        {
            return localStorage.AddVariable(key, initialBase);
        }

        public bool RemoveVariable(Variable key)
        {
            return localStorage.RemoveVariable(key);
        }

        internal bool ContainsModifiedValueCache(Variable key)
        {
            return localStorage.ContainsModifiedValueCache(key);
        }

        internal bool InstanceBagHasLocalKey(Variable key)
        {
            return localStorage.InstanceBagHasLocalKey(key);
        }

        internal bool TryResolveKeyByName(string name, out Variable key)
        {
            if (TryFindKeyInDefinition(name, out key))
            {
                return true;
            }

            if (TryFindKeyInBagLocalKeys(localStorage.InstanceBaseBag, name, out key))
            {
                return true;
            }

            return TryFindKeyInBagLocalKeys(localStorage.InstanceEffectiveBag, name, out key);
        }

        private bool TryFindKeyInDefinition(string name, out Variable key)
        {
            key = default!;
            if (definition == null)
            {
                return false;
            }

            foreach (Variable v in definition.DefinedVariables)
            {
                if (v.Key == name)
                {
                    key = v;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindKeyInBagLocalKeys(VariableBag bag, string name, out Variable key)
        {
            foreach (Variable k in bag.LocalKeys)
            {
                if (k.Key == name)
                {
                    key = k;
                    return true;
                }
            }

            key = default!;
            return false;
        }
    }
}
