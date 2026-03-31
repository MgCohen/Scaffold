using System;

namespace Scaffold.Scope.Contracts
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class AllowSameLayerInitializationUsageAttribute : Attribute
    {
    }
}

