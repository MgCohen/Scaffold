using System;
using VContainer;

namespace Scaffold.Containers
{
    internal sealed class VContainerRegistrationBuilder<T> : IRegistrationBuilder<T>
    {
        private readonly RegistrationBuilder inner;

        internal VContainerRegistrationBuilder(RegistrationBuilder inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRegistrationBuilder<T> WithParameter<TParam>(TParam value)
        {
            inner.WithParameter(value);
            return this;
        }

        public IRegistrationBuilder<T> AsImplementedInterfaces()
        {
            inner.AsImplementedInterfaces();
            return this;
        }
    }
}
