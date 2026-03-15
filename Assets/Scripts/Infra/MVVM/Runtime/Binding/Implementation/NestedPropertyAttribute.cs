using System;

namespace Scaffold.MVVM.Binding
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class NestedPropertyAttribute : Attribute
    {

    }
}

