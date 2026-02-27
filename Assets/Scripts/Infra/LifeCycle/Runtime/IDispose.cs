using UnityEngine;

namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Represents the disposal phase of a lifecycle component.
    /// The main goal is to provide an awaitable contract for releasing resources or tearing down a module.
    /// It is used by game systems to ensure proper cleanup, preventing memory leaks and dangling references.
    /// </summary>
    public interface IDispose
    {
        public Awaitable Dispose();
    }
}