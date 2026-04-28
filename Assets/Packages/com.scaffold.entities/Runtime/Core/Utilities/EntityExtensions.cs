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

        internal static void AddModifier<TDef, T>(this IMutableEntity<TDef> entity, string variableName, T value) where TDef : IEntityDefinition
        {
            Variable key = ResolveVariableByName(entity, variableName);
            VariableValue modifierPayload = VariableValueFactory.From(value);
            var entry = new EntityModifierEntry(key, modifierPayload);
            entity.AddModifier(entry);
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

        private static Variable ResolveVariableByName<TDef>(IMutableEntity<TDef> entity, string name) where TDef : IEntityDefinition
        {
            EntityInstance<TDef>? ei = entity as EntityInstance<TDef>;
            if (ei == null && entity is EntityComponent<TDef> component)
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
