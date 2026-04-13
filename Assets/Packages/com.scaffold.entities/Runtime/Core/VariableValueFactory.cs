using System;

namespace Scaffold.Entities
{
    internal static class VariableValueFactory
    {
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
