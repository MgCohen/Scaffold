using System;

namespace Scaffold.AutoPacker
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class AutoPackAttribute : Attribute { }
}
