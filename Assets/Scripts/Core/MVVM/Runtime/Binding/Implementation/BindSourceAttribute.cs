using System;

namespace Scaffold.MVVM.Binding
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class BindSourceAttribute : Attribute
    {
        public BindSourceAttribute(Type bindingType)
        {
            BindingType = bindingType;
        }

        public Type BindingType { get; }
    }
}
