#nullable enable
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IVariableBag
    {
        IVariableBag? Parent { get; }

        bool TryGetBase(Variable key, out VariableValue value);

        IEnumerable<Variable> LocalKeys { get; }
    }
}
