using System.Collections.Generic;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    public interface IEntityDefinition
    {
        bool TryGetDefaultValue(Variable key, out VariableValue value);

        IEnumerable<Variable> DefinedVariables { get; }
    }
}
