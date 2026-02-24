using System;

namespace Scaffold.AutoPacker
{
    [AttributeUsage(AttributeTargets.Field)]
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
