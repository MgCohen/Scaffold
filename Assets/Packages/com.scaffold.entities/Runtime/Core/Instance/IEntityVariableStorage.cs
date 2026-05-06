#nullable enable
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityVariableStorage
    {
        IEntityVariableStorage? Parent { get; }

        bool TryGetBase(Variable key, out VariableValue value);
        IEnumerable<ActiveModifier> GetModifiers(Variable key);
        IEnumerable<Variable> Variables { get; }

        bool AddVariable(Variable key, VariableValue initial);
        bool RemoveVariable(Variable key);
        bool SetBaseValue(Variable key, VariableValue value);
        ModifierId AddModifier(Variable key, VariableModifier mod, ModifierSource source = default, ModifierId? id = null);
        bool RemoveModifier(Variable key, ModifierId id);
        void ClearModifiers();
        void RemoveModifiersFromSource(ModifierSource source);
    }
}
