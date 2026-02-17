namespace Scaffold.Containers
{
    /// <summary>
    /// Project-level abstraction for resolving services from the container.
    /// </summary>
    public interface IContainerResolver
    {
        T Resolve<T>();
    }
}
