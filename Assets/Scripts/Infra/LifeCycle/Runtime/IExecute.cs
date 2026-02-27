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
        public Awaitable Execute();
    }
}
