using System;

namespace Scaffold.Entities
{
    internal static class VariableValueFactory
    {
        internal static VariableValue CreateDefault(VariableValueType type)
        {
            return type switch
            {
                VariableValueType.String => new StringVariableValue(),
                VariableValueType.Float => new FloatVariableValue(),
                VariableValueType.Int => new IntVariableValue(),
                VariableValueType.Bool => new BoolVariableValue(),
                _ => new StringVariableValue(),
            };
        }

        internal static VariableValue From<T>(T value)
        {
            return value switch
            {
                float f => new FloatVariableValue { Value = f },
                int n => new IntVariableValue { Value = n },
                bool b => new BoolVariableValue { Value = b },
                string s => new StringVariableValue { Value = s },
                _ => throw new NotSupportedException(
                    $"No VariableValue mapping for type {typeof(T).Name}.")
            };
        }
    }
}
