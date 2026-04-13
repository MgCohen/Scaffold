using System;

namespace Scaffold.Entities
{
    public interface IReadOnlyEntity<out TDefinition> where TDefinition : EntityDefinition
    {
        InstanceId Id { get; }

        TDefinition Definition { get; }

        T GetValue<T>(Variable key);

        TVar GetVariable<TVar>(Variable key) where TVar : VariableValue;

        bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue;

        IDisposable Subscribe(Variable key, Action<VariableValue> onChange);

        void Unsubscribe(Variable key, Action<VariableValue> onChange);

        IDisposable SubscribeToVariableAdded(Action<Variable, VariableValue> onAdded);

        IDisposable SubscribeToVariableRemoved(Action<Variable> onRemoved);
    }
}
