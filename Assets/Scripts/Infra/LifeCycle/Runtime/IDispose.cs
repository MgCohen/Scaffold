using System.Threading.Tasks;

namespace Scaffold.Infra.LifeCycle
{
    /// <summary>
    /// Represents the disposal phase of a lifecycle component.
    /// The main goal is to provide an awaitable contract for releasing resources or tearing down a module.
    /// It is used by game systems to ensure proper cleanup, preventing memory leaks and dangling references.
    /// </summary>
    public interface IDispose
    {
        /// <summary>
        /// Begins the disposal process.
        /// The main goal is to clean up resources asynchronously.
        /// It is used when a system shuts down or is destroyed.
        /// </summary>
        public Task Dispose();
    }
}