using UnityEngine;

namespace Scaffold.LifeCycle
{
    /// <summary>
    /// Represents the execution phase of a lifecycle component.
    /// The main goal is to provide an awaitable contract for running a system's core logic.
    /// It is used by the main game loop or task queues to periodically process operations within modules.
    /// </summary>
    public interface IExecute
    {
        /// <summary>
        /// Executes the core logic for the component.
        /// The main goal is to run periodic updates or operations asynchronously.
        /// It is used by the application's tick or execution loop.
        /// </summary>
        public Awaitable Execute();
    }
}
