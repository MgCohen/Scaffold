using UnityEngine;

namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Represents the initialization phase of a lifecycle component.
    /// The main goal is to provide an awaitable contract for setting up a module before it begins operating.
    /// It is used when bootstrapping game systems to ensure all asynchronous dependencies and preparations are complete.
    /// </summary>
    public interface IInitialize
    {
        public Awaitable Initialize();
    }
}