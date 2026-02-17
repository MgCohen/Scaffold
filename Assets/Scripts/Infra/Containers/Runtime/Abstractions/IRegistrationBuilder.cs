namespace Scaffold.Containers
{
    /// <summary>
    /// Abstraction over a single registration, supporting fluent configuration.
    /// </summary>
    /// <typeparam name="T">The component type being registered.</typeparam>
    public interface IRegistrationBuilder<T>
    {
        IRegistrationBuilder<T> WithParameter<TParam>(TParam value);

        IRegistrationBuilder<T> AsImplementedInterfaces();
    }
}

