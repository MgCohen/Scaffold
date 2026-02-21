using System;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SerializedAttribute : Attribute 
{ 
    public Type TargetType { get; }

    public SerializedAttribute() { }

    public SerializedAttribute(Type targetType)
    {
        TargetType = targetType;
    }
}
