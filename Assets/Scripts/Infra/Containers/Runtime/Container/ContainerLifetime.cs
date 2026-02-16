namespace Scaffold.Containers
{
    /// <summary>
    /// Lifetime options for services registered through the container abstraction.
    /// </summary>
    public enum ContainerLifetime
    {
        Singleton,
        Scoped,
        Transient
    }
}

