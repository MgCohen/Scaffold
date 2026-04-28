using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityVariableStorage
    {
        bool TryGetEffective(Variable key, out VariableValue value);

        bool TryGetBase(Variable key, out VariableValue value);

        IEnumerable<Variable> Variables { get; }

        IDisposable Subscribe(Variable key, Action<VariableValue> callback);

        void Unsubscribe(Variable key, Action<VariableValue> callback);

        IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler);
    }
}
