#nullable enable
using System.Collections.Generic;
using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class StoreVariableStorage : IEntityVariableStorage
    {
        private readonly Store store;
        private readonly Reference entityRef;

        public StoreVariableStorage(Store store, Reference entityRef)
        {
            this.store = store;
            this.entityRef = entityRef;
        }

        private EntityState Slice => store.Get<EntityState>(entityRef);

        public IEntityVariableStorage? Parent => null;

        public bool TryGetBase(Variable key, out VariableValue value) => Slice.TryGetBase(key, out value);

        public IEnumerable<ActiveModifier> GetModifiers(Variable key) => Slice.GetModifiers(key);

        public IEnumerable<Variable> Variables => Slice.Variables;

        public bool AddVariable(Variable key, VariableValue initial)
        {
            store.Execute(new AddEntityVariablePayload(entityRef, key, initial));
            return true;
        }

        public bool RemoveVariable(Variable key)
        {
            store.Execute(new RemoveEntityVariablePayload(entityRef, key));
            return true;
        }

        public bool SetBaseValue(Variable key, VariableValue value)
        {
            store.Execute(new SetBaseValuePayload(entityRef, key, value));
            return true;
        }

        public ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null)
        {
            ModifierId resolvedId = id ?? ModifierId.New();
            store.Execute(new AddModifierPayload(entityRef, key, mod, resolvedId, source));
            return resolvedId;
        }

        public bool RemoveModifier(Variable key, ModifierId id)
        {
            store.Execute(new RemoveModifierPayload(entityRef, key, id));
            return true;
        }

        public void ClearModifiers()
        {
            store.Execute(new ClearModifiersPayload(entityRef));
        }

        public void RemoveModifiersFromSource(ModifierSource source)
        {
            store.Execute(new RemoveModifiersBySourcePayload(entityRef, source));
        }
    }
}
