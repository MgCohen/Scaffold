using System;

namespace Scaffold.AutoPacker
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
    public sealed class AutoPackAttribute : Attribute
    {
        // Reference type: a [Packed] field of this type stores the type's single [Packed] key
        // member, and unpack resolves the key back to an existing object through IPackingHandler.
        // The type itself gets no generated Packed struct.
        public bool AsReference { get; set; }
    }
}
