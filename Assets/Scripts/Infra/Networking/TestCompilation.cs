using System;
using Unity.Netcode;

public struct EquatableWrapper<T> : IEquatable<EquatableWrapper<T>> where T : unmanaged
{
    public T Value;
    public EquatableWrapper(T value) { Value = value; }
    public bool Equals(EquatableWrapper<T> other) => Value.Equals(other.Value);
}

public static class TestSer
{
    public static void Test<T>(T message) where T : unmanaged
    {
        if (message is INetworkSerializable ser) 
        {
        }
        else if (message is IEquatable<T> eq)
        {
            // Will this compile?
            // var w = new ForceNetworkSerializeByMemcpy<T>(message); 
        }
        else 
        {
            var wrapper = new EquatableWrapper<T>(message);
            var w = new ForceNetworkSerializeByMemcpy<EquatableWrapper<T>>(wrapper);
        }
    }
}
