#nullable enable
using System;

namespace Scaffold.Entities
{
    public interface IReadOnlyEntity<out TDefinition> where TDefinition : IEntityDefinition
    {
        InstanceId Id { get; }

        T GetVariable<T>(Variable key);

        bool TryGetVariable<T>(Variable key, out T value);

        IDisposable Subscribe(Variable key, Action<VariableValue> onChange);

        void Unsubscribe(Variable key, Action<VariableValue> onChange);

        IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler);
    }
}
