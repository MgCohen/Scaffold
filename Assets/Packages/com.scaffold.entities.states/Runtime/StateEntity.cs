#nullable enable

using System;
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record StateEntity<TDefinition>(InstanceId Id, TDefinition Definition, IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks, IReadOnlyDictionary<Variable, VariableValue> EffectiveValues) : AggregateState where TDefinition : IEntityDefinition
    {
        public T GetVariable<T>(Variable key)
        {
            if (!TryGetVariable<T>(key, out var value))
            {
                throw new InvalidOperationException($"Variable '{key?.Key ?? "?"}' is not defined on this entity.");
            }

            return value;
        }

        public bool TryGetVariable<T>(Variable key, out T value)
        {
            value = default!;
            if (key == null)
            {
                return false;
            }

            return TryReadEffectiveAs(key, out value) || TryReadBaseAs(key, out value) || TryReadDefaultAs(key, out value);
        }

        private bool TryReadEffectiveAs<T>(Variable key, out T value)
        {
            if (EffectiveValues.TryGetValue(key, out VariableValue? ev) && ev is IVariableValue<T> typedE)
            {
                value = typedE.Get();
                return true;
            }

            value = default!;
            return false;
        }

        private bool TryReadBaseAs<T>(Variable key, out T value)
        {
            if (BaseValues.TryGetValue(key, out VariableValue? bv) && bv is IVariableValue<T> typedB)
            {
                value = typedB.Get();
                return true;
            }

            value = default!;
            return false;
        }

        private bool TryReadDefaultAs<T>(Variable key, out T value)
        {
            if (Definition.TryGetDefaultValue(key, out VariableValue? dv) && dv is IVariableValue<T> typedD)
            {
                value = typedD.Get();
                return true;
            }

            value = default!;
            return false;
        }
    }
}
