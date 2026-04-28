using System;

namespace Scaffold.Entities
{
    internal static class EntityExtensions
    {
        internal static IDisposable Subscribe<TDef, T>(this IReadOnlyEntity<TDef> entity, Variable key, Action<T> onChange) where TDef : IEntityDefinition
        {
            return entity.Subscribe(key, av => DispatchSubscribePrimitive(av, onChange));
        }

        internal static IDisposable SubscribeToVariable<TDef, TVar>(this IReadOnlyEntity<TDef> entity, Variable key, Action<TVar> onChange) where TDef : IEntityDefinition where TVar : VariableValue
        {
            return entity.Subscribe(key, av => DispatchSubscribeVariable(av, onChange));
        }

        internal static bool AddVariable<TDef, T>(this IMutableEntity<TDef> entity, Variable key, T initialValue) where TDef : IEntityDefinition
        {
            return entity.AddVariable(key, VariableValueFactory.From(initialValue));
        }

        internal static void AddModifier<TDef, T>(this IMutableEntity<TDef> entity, Variable key, T value) where TDef : IEntityDefinition
        {
            VariableValue modifierPayload = VariableValueFactory.From(value);
            var entry = new EntityModifierEntry(key, modifierPayload);
            entity.AddModifier(entry);
        }

        internal static void AddModifier<TDef, T>(this IMutableEntity<TDef> entity, string name, VariableValueType type, T value) where TDef : IEntityDefinition
        {
            entity.AddModifier(new Variable(name, type), value);
        }

        private static void DispatchSubscribePrimitive<T>(VariableValue av, Action<T> onChange)
        {
            if (av is IVariableValue<T> typed)
            {
                onChange(typed.Get());
            }
        }

        private static void DispatchSubscribeVariable<TVar>(VariableValue av, Action<TVar> onChange) where TVar : VariableValue
        {
            if (av is TVar typed)
            {
                onChange(typed);
            }
        }
    }
}
