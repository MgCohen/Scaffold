#nullable enable
using System.Collections.Generic;
using System.Linq;
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

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            if (Slice.BaseValues.TryGetValue(key, out var bv) && bv != null)
            {
                value = bv;
                return true;
            }
            value = default!;
            return false;
        }

        public IEnumerable<ActiveModifier> GetModifiers(Variable key)
        {
            if (Slice.ModifierStacks.TryGetValue(key, out var bucket) && bucket != null)
            {
                return bucket.OrderBy(m => m.Modifier.Order);
            }
            return System.Array.Empty<ActiveModifier>();
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                var s = Slice;
                var seen = new HashSet<Variable>(s.BaseValues.Keys);
                foreach (var k in s.ModifierStacks.Keys) seen.Add(k);
                return seen;
            }
        }

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
            var s = Slice;
            foreach (var kv in s.ModifierStacks)
            {
                foreach (var mod in kv.Value)
                {
                    store.Execute(new RemoveModifierPayload(entityRef, kv.Key, mod.Id));
                }
            }
        }

        public void RemoveModifiersFromSource(ModifierSource source)
        {
            store.Execute(new RemoveModifiersBySourcePayload(entityRef, source));
        }
    }
}
