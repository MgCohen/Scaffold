using System;
using VContainer;

namespace Scaffold.Containers
{
    internal sealed class VContainerRegistrationBuilder<T> : IRegistrationBuilder<T>
    {
        private readonly RegistrationBuilder _inner;

        internal VContainerRegistrationBuilder(RegistrationBuilder inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IRegistrationBuilder<T> WithParameter<TParam>(TParam value)
        {
            _inner.WithParameter(value);
            return this;
        }

        public IRegistrationBuilder<T> AsImplementedInterfaces()
        {
            _inner.AsImplementedInterfaces();
            return this;
        }
    }
}
