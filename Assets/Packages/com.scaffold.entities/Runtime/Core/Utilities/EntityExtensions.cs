using System;

namespace Scaffold.Entities
{
    internal static class EntityExtensions
    {
        internal static bool AddVariable<TDef, T>(this EntityInstance<TDef> entity, Variable key, T initialValue) where TDef : IEntityDefinition
        {
            return entity.AddVariable(key, VariableValueFactory.From(initialValue));
        }

        internal static void AddModifier<TDef, T>(this EntityInstance<TDef> entity, Variable key, T value) where TDef : IEntityDefinition
        {
            VariableModifier modifier = CreateModifierFromPrimitive(value);
            entity.AddModifier(key, modifier);
        }

        internal static void AddModifier<TDef, T>(this EntityInstance<TDef> entity, string name, string payloadTypeId, T value) where TDef : IEntityDefinition
        {
            entity.AddModifier(new Variable(name, payloadTypeId), value);
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
                _ => throw new NotSupportedException($"No VariableModifier mapping for type {typeof(T).Name}.")
            };
        }
    }
}
