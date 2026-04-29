using System.Collections.Immutable;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record EntityVariableState(
        ImmutableDictionary<Variable, VariableValue> BaseValues,
        ImmutableDictionary<Variable, ImmutableList<ActiveModifier>> ModifierStacks,
        ImmutableDictionary<Variable, VariableValue> EffectiveValues
    ) : State
    {
        public static EntityVariableState Empty { get; } = new EntityVariableState(
            ImmutableDictionary<Variable, VariableValue>.Empty,
            ImmutableDictionary<Variable, ImmutableList<ActiveModifier>>.Empty,
            ImmutableDictionary<Variable, VariableValue>.Empty);
    }
}
