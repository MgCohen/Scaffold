namespace Scaffold.Containers.Adapters
{
    /// <summary>
    /// Fallback registration builder used when the underlying API does not return a builder.
    /// Methods are effectively no-ops but keep the fluent interface consistent.
    /// </summary>
    internal sealed class NoOpRegistrationBuilderAdapter<T> : IRegistrationBuilder<T>
    {
        public IRegistrationBuilder<T> WithParameter<TParam>(TParam value) => this;

        public IRegistrationBuilder<T> AsImplementedInterfaces() => this;
    }
}

