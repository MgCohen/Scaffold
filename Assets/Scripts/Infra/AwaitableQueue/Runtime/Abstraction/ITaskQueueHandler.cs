using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Defines the execution and management contract for a queue of awaitable tasks.
    /// The main goal is to provide controls for registering, pausing, and awaiting batched asynchronous operations.
    /// It is used by service managers that orchestrate operations, ensuring tasks are processed serially or reliably.
    /// </summary>
    public interface ITaskQueueHandler
    {
        bool HasHistory { get; }
        event Action<Awaitable> OnTaskExecuting;
        void Pause();
        void Resume();
        Awaitable WaitForAllTasksToFinish();
        void RegisterTask(Awaitable task);
        void RegisterTasks(params Awaitable[] tasks);
        void ClearTasks();
    }
}
