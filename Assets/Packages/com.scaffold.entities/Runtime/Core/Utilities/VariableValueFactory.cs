using System;

namespace Scaffold.Entities
{
    internal static class VariableValueFactory
    {
        internal static VariableValue CreateDefault(Type payloadType)
        {
            if (payloadType == null || !VariableValueRegistry.Contains(payloadType))
            {
                throw new ArgumentException(
                    $"Type {payloadType} is not a registered VariableValue. " +
                    $"Concrete subclasses must declare [VariableValueId(\"...\")].",
                    nameof(payloadType));
            }

            return (VariableValue)Activator.CreateInstance(payloadType)!;
        }

        internal static VariableValue CreateDefault(string payloadTypeId)
        {
            if (!VariableValueRegistry.TryResolve(payloadTypeId, out Type t))
            {
                throw new ArgumentException($"Unknown VariableValue payload id: '{payloadTypeId}'.");
            }

            return CreateDefault(t);
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
