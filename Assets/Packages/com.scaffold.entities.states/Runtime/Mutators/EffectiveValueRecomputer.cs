#nullable enable
using System.Collections.Immutable;

using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    internal static class EffectiveValueRecomputer
    {
        public static ImmutableDictionary<Variable, VariableValue> RecomputeFor(
            EntityVariableState state,
            ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> nextStacks,
            Variable variable,
            IEntityDefinition definition)
        {
            VariableValue? baseValue = ResolveBase(state.BaseValues, variable, definition);
            if (baseValue == null)
            {
                return state.EffectiveValues.Remove(variable);
            }

            if (!nextStacks.TryGetValue(variable, out var bucket) || bucket.Count == 0)
            {
                return state.EffectiveValues.Remove(variable);
            }

            VariableValue effective = baseValue.ApplyModifiers(bucket);
            return state.EffectiveValues.SetItem(variable, effective);
        }

        private static VariableValue? ResolveBase(
            ImmutableDictionary<Variable, VariableValue> baseValues,
            Variable variable,
            IEntityDefinition definition)
        {
            if (baseValues.TryGetValue(variable, out var bv))
            {
                return bv;
            }

            return definition.TryGetDefaultValue(variable, out var dv) ? dv : null;
        }
    }
}
