using System;

namespace Scaffold.Scope.Contracts
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class AllowInitializationCallChainAttribute : Attribute
    {
    }
}

