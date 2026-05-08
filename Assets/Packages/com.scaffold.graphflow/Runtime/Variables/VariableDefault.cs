using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class VariableDefault
    {
        public abstract Type ValueType { get; }
        public abstract VariableCell CreateCell(string id);
    }

    [Serializable]
    public abstract class VariableDefault<T> : VariableDefault
    {
        public T value;

        public sealed override Type ValueType => typeof(T);
        public sealed override VariableCell CreateCell(string id) => new VariableCell<T>(id, value);
    }

    [Serializable] public sealed class IntDefault    : VariableDefault<int> { }
    [Serializable] public sealed class FloatDefault  : VariableDefault<float> { }
    [Serializable] public sealed class BoolDefault   : VariableDefault<bool> { }
    [Serializable] public sealed class StringDefault : VariableDefault<string> { }
    [Serializable] public sealed class ObjectDefault : VariableDefault<UnityEngine.Object> { }
}
