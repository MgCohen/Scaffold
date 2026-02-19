namespace Scaffold.Containers
{
    internal sealed class NoOpRegistrationBuilder<T> : IRegistrationBuilder<T>
    {
        public IRegistrationBuilder<T> WithParameter<TParam>(TParam value) => this;

        public IRegistrationBuilder<T> AsImplementedInterfaces() => this;
    }
}
