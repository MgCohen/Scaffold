#nullable enable

using System;
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>, IMutableEntity<TDefinition>, IDisposable where TDefinition : IEntityDefinition
    {
        private Store store = default!;
        private StoreVariableStorage storeStorage = default!;

        internal void InitializeStateBacked(InstanceId id, TDefinition definition, Store store, StoreVariableStorage storage)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            storeStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            Initialize(id, definition, storage);
        }

        public void Dispose()
        {
            storeStorage?.Dispose();
            storeStorage = null!;
            store = null!;
        }

        public bool AddVariable(Variable key, VariableValue initialBase)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (initialBase == null)
            {
                throw new ArgumentNullException(nameof(initialBase));
            }

            if (store.Get<EntityVariableState>(Id).BaseValues.ContainsKey(key))
            {
                return false;
            }

            store.Execute(Id, new AddEntityVariablePayload(Id, key, initialBase));
            return true;
        }

        public bool RemoveVariable(Variable key)
        {
            EntityVariableState snapshot = store.Get<EntityVariableState>(Id);
            bool wasPresent = snapshot.BaseValues.ContainsKey(key) || snapshot.ModifierStacks.ContainsKey(key);
            if (!wasPresent)
            {
                return false;
            }

            store.Execute(Id, new RemoveEntityVariablePayload(Id, key));
            return true;
        }

        public ModifierId AddModifier(EntityModifierEntry entry)
        {
            ModifierId modifierId = ModifierId.New();
            store.Execute(Id, new AddModifierPayload(Id, entry.Key, entry.Modifier, modifierId));
            return modifierId;
        }

        public bool RemoveModifier(Variable key, ModifierId id)
        {
            store.Execute(Id, new RemoveModifierPayload(Id, key, id));
            return true;
        }

        public void ClearModifiers()
        {
            EntityVariableState snapshot = store.Get<EntityVariableState>(Id);
            var payloads = new List<object>();
            foreach (KeyValuePair<Variable, IReadOnlyList<ActiveModifier>> kv in snapshot.ModifierStacks)
            {
                foreach (ActiveModifier active in kv.Value)
                {
                    payloads.Add(new RemoveModifierPayload(Id, kv.Key, active.Id));
                }
            }

            if (payloads.Count > 0)
            {
                store.ExecuteBatch(payloads);
            }
        }
    }
}
