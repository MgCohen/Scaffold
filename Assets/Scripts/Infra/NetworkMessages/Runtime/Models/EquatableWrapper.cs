using System;
using System.Collections.Generic;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// A generic wrapper struct that ensures our type is IEquatable for ForceNetworkSerializeByMemcpy.
    /// This is used for types that do not implement INetworkSerializable natively to ensure safe serialization.
    /// </summary>
    public struct EquatableWrapper<T> : IEquatable<EquatableWrapper<T>> where T : unmanaged
    {
        public T Value;

        public EquatableWrapper(T value)
        {
            Value = value;
        }

        public bool Equals(EquatableWrapper<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }
    }
}
