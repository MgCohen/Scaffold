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

        internal static bool AddVariable<TDef, T>(this IEntity<TDef> entity, Variable key, T initialValue) where TDef : IEntityDefinition
        {
            return entity.AddVariable(key, VariableValueFactory.From(initialValue));
        }

        internal static void AddModifier<TDef, T>(this IInstance<TDef> instance, Variable key, T value) where TDef : IEntityDefinition
        {
            VariableValue modifierPayload = VariableValueFactory.From(value);
            var entry = new EntityModifierEntry(key, modifierPayload);
            instance.AddModifier(entry);
        }

        internal static void AddModifier<TDef, T>(this IInstance<TDef> instance, string variableName, T value) where TDef : IEntityDefinition
        {
            Variable key = ResolveVariableByName(instance, variableName);
            VariableValue modifierPayload = VariableValueFactory.From(value);
            var entry = new EntityModifierEntry(key, modifierPayload);
            instance.AddModifier(entry);
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

        private static Variable ResolveVariableByName<TDef>(IInstance<TDef> instance, string name) where TDef : IEntityDefinition
        {
            EntityInstance<TDef>? ei = instance as EntityInstance<TDef>;
            if (ei == null && instance is EntityComponent<TDef> component)
            {
                ei = component.Instance;
            }

            if (ei != null && ei.TryResolveKeyByName(name, out Variable key))
            {
                return key;
            }

            throw new InvalidOperationException(
                $"No variable with name '{name}' exists on this entity.");
        }
    }
}
