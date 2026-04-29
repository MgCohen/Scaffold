using System;
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class StoreVariableStorage : IEntityVariableStorage
    {
        private readonly Store store;
        private readonly InstanceId instanceId;
        private readonly IEntityDefinition definition;

        private readonly Dictionary<Variable, List<Action<VariableValue>>> perVariable = new();
        private readonly List<Action<VariableStructuralChange, Variable, VariableValue?>> structural = new();

        public StoreVariableStorage(Store store, InstanceId instanceId, IEntityDefinition definition)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.instanceId = instanceId;
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));

            store.Subscribe<EntityVariableState>(instanceId, OnSliceChanged);
        }

        public bool TryGetEffective(Variable key, out VariableValue value)
        {
            var state = store.Get<EntityVariableState>(instanceId);
            if (state.EffectiveValues.TryGetValue(key, out value))
            {
                return true;
            }

            if (state.BaseValues.TryGetValue(key, out value))
            {
                return true;
            }

            return definition.TryGetDefaultValue(key, out value);
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            var state = store.Get<EntityVariableState>(instanceId);
            if (state.BaseValues.TryGetValue(key, out value))
            {
                return true;
            }

            return definition.TryGetDefaultValue(key, out value);
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                var state = store.Get<EntityVariableState>(instanceId);
                foreach (var key in definition.DefinedVariables)
                {
                    yield return key;
                }

                foreach (var key in state.BaseValues.Keys)
                {
                    if (!definition.TryGetDefaultValue(key, out _))
                    {
                        yield return key;
                    }
                }
            }
        }

        public IDisposable Subscribe(Variable key, Action<VariableValue> callback)
        {
            if (key == null || callback == null)
            {
                return EmptyDisposable.Instance;
            }

            if (!perVariable.TryGetValue(key, out var list))
            {
                list = new List<Action<VariableValue>>();
                perVariable[key] = list;
            }

            list.Add(callback);
            if (TryGetEffective(key, out var current))
            {
                callback(current);
            }

            return new VariableSubscription(this, key, callback);
        }

        public void Unsubscribe(Variable key, Action<VariableValue> callback)
        {
            if (key == null || callback == null)
            {
                return;
            }

            if (!perVariable.TryGetValue(key, out var list))
            {
                return;
            }

            list.Remove(callback);
            if (list.Count == 0)
            {
                perVariable.Remove(key);
            }
        }

        public IDisposable SubscribeToVariableStructuralChanges(
            Action<VariableStructuralChange, Variable, VariableValue?> handler)
        {
            if (handler == null)
            {
                return EmptyDisposable.Instance;
            }

            structural.Add(handler);
            return new StructuralSubscription(this, handler);
        }

        private void OnSliceChanged(IReference reference, EntityVariableState next, StateChangeEvent ev)
        {
            foreach (var pair in perVariable)
            {
                if (TryGetEffective(pair.Key, out var current))
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        pair.Value[i](current);
                    }
                }
            }
        }

        private sealed class VariableSubscription : IDisposable
        {
            private StoreVariableStorage owner;
            private Variable key;
            private Action<VariableValue> callback;

            public VariableSubscription(StoreVariableStorage o, Variable k, Action<VariableValue> c)
            {
                owner = o;
                key = k;
                callback = c;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.Unsubscribe(key, callback);
                owner = null;
                key = null!;
                callback = null!;
            }
        }

        private sealed class StructuralSubscription : IDisposable
        {
            private StoreVariableStorage owner;
            private Action<VariableStructuralChange, Variable, VariableValue?> handler;

            public StructuralSubscription(
                StoreVariableStorage o,
                Action<VariableStructuralChange, Variable, VariableValue?> h)
            {
                owner = o;
                handler = h;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.structural.Remove(handler);
                owner = null;
                handler = null!;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose()
            {
            }
        }
    }
}
