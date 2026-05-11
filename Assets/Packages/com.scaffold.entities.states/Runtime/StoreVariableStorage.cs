#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Scaffold.Entities;
using Scaffold.States;
using Scaffold.Variables;

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
        IVariableBag? IVariableBag.Parent => null;

        public bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle)
        {
            var lookupKey = new Variable(id, "");
            if (Slice.TryGetBase(lookupKey, out var baseVal) && baseVal is IVariableValue<T>)
            {
                handle = new EntityVariableHandle<T>(id,
                    () =>
                    {
                        if (!Slice.TryGetBase(lookupKey, out var anchor) || !(anchor is IVariableValue<T> typedAnchor))
                            return default!;
                        T result = typedAnchor.Get();
                        foreach (var mod in Slice.GetModifiers(lookupKey))
                        {
                            if (mod.Modifier is VariableModifier<T> typedMod)
                                result = typedMod.Apply(result);
                        }
                        return result;
                    },
                    newVal =>
                    {
                        if (Slice.TryGetBase(lookupKey, out var existing) && existing is VariableValue<T> ex)
                            SetBaseValue(lookupKey, ex.CreateWithValue(newVal));
                    });
                return true;
            }
            handle = null;
            return false;
        }

        public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            var lookupKey = new Variable(id, "");
            if (Slice.TryGetBase(lookupKey, out var val) && val != null)
            {
                handle = new EntityVariableHandle(id, EntityVariableHandle.ResolvePayloadType(val));
                return true;
            }
            handle = null;
            return false;
        }

        public IEnumerable<IVariableHandle> LocalHandles
        {
            get
            {
                foreach (var kvp in Slice.BaseValues)
                {
                    if (kvp.Value != null)
                        yield return new EntityVariableHandle(kvp.Key.Id, EntityVariableHandle.ResolvePayloadType(kvp.Value));
                }
            }
        }

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
