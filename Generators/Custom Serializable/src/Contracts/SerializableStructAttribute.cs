using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SerializableStructAttribute : Attribute { }
