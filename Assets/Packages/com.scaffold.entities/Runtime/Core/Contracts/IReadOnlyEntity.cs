using System;

namespace Scaffold.Entities
{
    public interface IReadOnlyEntity<out TDefinition> where TDefinition : IEntityDefinition
    {
        InstanceId Id { get; }

        T GetValue<T>(Variable key);

        bool TryGetValue<T>(Variable key, out T value);

        TVar GetVariable<TVar>(Variable key) where TVar : VariableValue;

        bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue;

        IDisposable Subscribe(Variable key, Action<VariableValue> onChange);

        void Unsubscribe(Variable key, Action<VariableValue> onChange);

        IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler);
    }
}
