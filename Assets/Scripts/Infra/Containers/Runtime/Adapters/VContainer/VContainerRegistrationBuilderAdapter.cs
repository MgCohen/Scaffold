using System;
using VContainer;

namespace Scaffold.Containers.Adapters
{
    /// <summary>
    /// Adapter that wraps VContainer's RegistrationBuilder fluent API.
    /// </summary>
    /// <typeparam name="T">The component type being registered.</typeparam>
    internal sealed class VContainerRegistrationBuilderAdapter<T> : IRegistrationBuilder<T>
    {
        private readonly RegistrationBuilder _inner;

        public VContainerRegistrationBuilderAdapter(RegistrationBuilder inner)
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

