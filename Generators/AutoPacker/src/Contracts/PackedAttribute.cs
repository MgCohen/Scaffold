using System;

namespace Scaffold.AutoPacker
{
    // Properties are read only as the key member of an [AutoPack(AsReference = true)] type.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PackedAttribute : Attribute
    {
        public Type TargetType { get; }

        public PackedAttribute() { }

        public PackedAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }
}
