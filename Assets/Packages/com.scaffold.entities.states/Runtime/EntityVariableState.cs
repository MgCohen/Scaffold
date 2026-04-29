using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record EntityVariableState(IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks, IReadOnlyDictionary<Variable, VariableValue> EffectiveValues) : State
    {
        public static EntityVariableState Empty { get; } = new EntityVariableState(new Dictionary<Variable, VariableValue>(), new Dictionary<Variable, IReadOnlyList<ActiveModifier>>(), new Dictionary<Variable, VariableValue>());

        internal static Dictionary<Variable, VariableValue> CreateMutableValues(IReadOnlyDictionary<Variable, VariableValue> source)
        {
            var copy = new Dictionary<Variable, VariableValue>(source.Count);
            foreach (var kv in source)
            {
                copy[kv.Key] = kv.Value;
            }

            return copy;
        }

        internal static Dictionary<Variable, IReadOnlyList<ActiveModifier>> CreateMutableStacks(IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> source)
        {
            var copy = new Dictionary<Variable, IReadOnlyList<ActiveModifier>>(source.Count);
            foreach (var kv in source)
            {
                copy[kv.Key] = kv.Value;
            }

            return copy;
        }
    }
}
