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
            VariableModifier modifier = CreateModifierFromPrimitive(value);
            entity.AddModifier(new EntityModifierEntry(key, modifier));
        }

        internal static void AddModifier<TDef, T>(this IMutableEntity<TDef> entity, string name, string payloadTypeId, T value) where TDef : IEntityDefinition
        {
            entity.AddModifier(new Variable(name, payloadTypeId), value);
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

        private static VariableModifier CreateModifierFromPrimitive<T>(T value)
        {
            object boxed = value!;
            return boxed switch
            {
                float f => new FloatAddModifier(f),
                int n => new IntAddModifier(n),
                bool b => new BoolOverrideModifier(b),
                string s => new StringAppendModifier(s),
                _ => throw new NotSupportedException(
                    $"No VariableModifier mapping for type {typeof(T).Name}.")
            };
        }
    }
}
