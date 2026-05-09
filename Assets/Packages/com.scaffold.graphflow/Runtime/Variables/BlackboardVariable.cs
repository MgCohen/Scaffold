using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class BlackboardVariable
    {
        public abstract Type ValueType { get; }
        public abstract VariableCell CreateCell(string id);
    }

    [Serializable]
    public abstract class BlackboardVariable<T> : BlackboardVariable
    {
        public T value;

        public sealed override Type ValueType => typeof(T);
        public sealed override VariableCell CreateCell(string id) => new VariableCell<T>(id, value);
    }

    [Serializable] public sealed class BlackboardInt    : BlackboardVariable<int> { }
    [Serializable] public sealed class BlackboardFloat  : BlackboardVariable<float> { }
    [Serializable] public sealed class BlackboardBool   : BlackboardVariable<bool> { }
    [Serializable] public sealed class BlackboardString : BlackboardVariable<string> { }
    [Serializable] public sealed class BlackboardObject : BlackboardVariable<UnityEngine.Object> { }
}
