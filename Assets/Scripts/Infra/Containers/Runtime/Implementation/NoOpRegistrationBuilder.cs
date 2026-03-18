namespace Scaffold.Containers
{
    internal sealed class NoOpRegistrationBuilder<T> : IRegistrationBuilder<T>
    {
        public IRegistrationBuilder<T> WithParameter<TParam>(TParam value)
        {
            return this;
        }

        public IRegistrationBuilder<T> AsImplementedInterfaces()
        {
            return this;
        }

        public IRegistrationBuilder<T> AsSelf()
        {
            return this;
        }
    }
}
