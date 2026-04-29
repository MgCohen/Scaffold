using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record EntityVariableState(Dictionary<Variable, VariableValue> BaseValues, Dictionary<Variable, List<ActiveModifier>> ModifierStacks, Dictionary<Variable, VariableValue> EffectiveValues) : State
    {
        public static EntityVariableState Empty { get; } = new EntityVariableState(new Dictionary<Variable, VariableValue>(), new Dictionary<Variable, List<ActiveModifier>>(), new Dictionary<Variable, VariableValue>());

        internal static Dictionary<Variable, VariableValue> CreateNewBaseDictionary(Dictionary<Variable, VariableValue> source)
        {
            return new Dictionary<Variable, VariableValue>(source);
        }

        internal static Dictionary<Variable, VariableValue> CreateNewEffectiveDictionary(Dictionary<Variable, VariableValue> source)
        {
            return new Dictionary<Variable, VariableValue>(source);
        }

        internal static Dictionary<Variable, List<ActiveModifier>> CreateNewModifierStacksDictionary(Dictionary<Variable, List<ActiveModifier>> source)
        {
            var copy = new Dictionary<Variable, List<ActiveModifier>>(source.Count);
            foreach (KeyValuePair<Variable, List<ActiveModifier>> kv in source)
            {
                copy[kv.Key] = new List<ActiveModifier>(kv.Value);
            }

            return copy;
        }
    }
}
