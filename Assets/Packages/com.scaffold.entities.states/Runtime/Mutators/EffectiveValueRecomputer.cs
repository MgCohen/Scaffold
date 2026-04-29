#nullable enable
using System.Collections.Generic;

using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    internal static class EffectiveValueRecomputer
    {
        public static Dictionary<Variable, VariableValue> RecomputeFor(Dictionary<Variable, VariableValue> baseValues, Dictionary<Variable, List<ActiveModifier>> modifierStacks, Dictionary<Variable, VariableValue> effectiveValuesSnapshot, Variable variable, IEntityDefinition definition)
        {
            var nextEffective = new Dictionary<Variable, VariableValue>(effectiveValuesSnapshot);
            VariableValue? baseValue = ResolveBase(baseValues, variable, definition);
            if (baseValue == null)
            {
                nextEffective.Remove(variable);
                return nextEffective;
            }

            if (!modifierStacks.TryGetValue(variable, out List<ActiveModifier>? bucket) || bucket == null || bucket.Count == 0)
            {
                nextEffective.Remove(variable);
                return nextEffective;
            }

            nextEffective[variable] = baseValue.ApplyModifiers(bucket);
            return nextEffective;
        }

        private static VariableValue? ResolveBase(Dictionary<Variable, VariableValue> baseValues, Variable variable, IEntityDefinition definition)
        {
            if (baseValues.TryGetValue(variable, out VariableValue? bv)) return bv;
            return definition.TryGetDefaultValue(variable, out VariableValue? dv) ? dv : null;
        }
    }
}
