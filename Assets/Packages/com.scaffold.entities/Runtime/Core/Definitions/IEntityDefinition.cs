using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityDefinition
    {
        bool TryGetDefaultValue(Variable key, out VariableValue value);

        IEnumerable<Variable> DefinedVariables { get; }
    }
}
