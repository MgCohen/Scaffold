using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class VariableValue<T> : VariableValue, IVariableValue<T>
    {
        public sealed override VariableValue ApplyModifiers(IReadOnlyList<ActiveModifier> modifiers)
        {
            T value = Get();
            for (int i = 0; i < modifiers.Count; i++)
            {
                value = ((VariableModifier<T>)modifiers[i].Modifier).Apply(value);
            }

            return WithValue(value);
        }

        public abstract T Get();

        internal VariableValue<T> CreateWithValue(T next) => WithValue(next);

        protected abstract VariableValue<T> WithValue(T value);
    }
}
