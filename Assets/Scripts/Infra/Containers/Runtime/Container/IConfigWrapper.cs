namespace Scaffold.Containers
{
    /// <summary>
    /// Wraps an inner IConfig; used so ContainerConfig can unwrap when resolving.
    /// </summary>
    public interface IConfigWrapper : IConfig
    {
        IConfig Config { get; }
    }
}
