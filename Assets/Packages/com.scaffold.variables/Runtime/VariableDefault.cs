#nullable enable
using System;

namespace Scaffold.Variables
{
    // Polymorphic seed-value record. Each package ships its own subclass
    // (graphflow's BlackboardVariable<T>, etc.) carrying a typed default value
    // that the bag uses to materialize a handle. SerializeReference-friendly:
    // concrete subclasses are [Serializable] and Unity discovers them
    // automatically when authored in the inspector.
    [Serializable]
    public abstract class VariableDefault
    {
        public abstract Type ValueType { get; }
        public abstract IVariableHandle CreateHandle(string id);
    }

    [Serializable]
    public abstract class VariableDefault<T> : VariableDefault
    {
        public T value = default!;

        public sealed override Type ValueType => typeof(T);

        public override IVariableHandle CreateHandle(string id)
            => new InMemoryHandle<T>(id, value);
    }
}
