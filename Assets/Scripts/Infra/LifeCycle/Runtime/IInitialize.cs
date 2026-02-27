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
        /// <summary>
        /// Initializes the component before it starts operating.
        /// The main goal is to trigger setup operations asynchronously.
        /// It is used during the bootstrap phase of the game module or system.
        /// </summary>
        public Awaitable Initialize();
    }
}