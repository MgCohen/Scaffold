using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Utility.AwaitableQueue
{
    /// <summary>
    /// Defines the execution and management contract for a queue of awaitable tasks.
    /// The main goal is to provide controls for registering, pausing, and awaiting batched asynchronous operations.
    /// It is used by service managers that orchestrate operations, ensuring tasks are processed serially or reliably.
    /// </summary>
    public interface ITaskQueueHandler
    {
        /// <summary>
        /// Indicates if there are previously executed operations in history.
        /// The main goal is to query the handler's lifetime past log.
        /// It is used to determine if a stream of queues has run.
        /// </summary>
        bool HasHistory { get; }

        /// <summary>
        /// Event fired immediately before a task is executed.
        /// The main goal is to intercept and track in-flight actions.
        /// It is used by debuggers or visual indicators bridging system activity.
        /// </summary>
        event Action<Task> OnTaskExecuting;
        /// <summary>
        /// Halts the sequential execution of pending tasks.
        /// The main goal is to freeze operations safely between events.
        /// It is used when the system requires user input or forced yields.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes execution of halted tasks in the queue.
        /// The main goal is to restore the operational sequence.
        /// It is used globally to unblock halted handlers.
        /// </summary>
        void Resume();

        /// <summary>
        /// Asynchronously blocks until the interior queue is drained.
        /// The main goal is to ensure dependencies finish completely.
        /// It is used gracefully in tear-down scripts.
        /// </summary>
        Task WaitForAllTasksToFinish();
        /// <summary>
        /// Enqueues a single awaitable action.
        /// The main goal is to safely pipe the execution step without threading errors.
        /// It is used continually when systems append background tasks.
        /// </summary>
        void RegisterTask(Task task);

        /// <summary>
        /// Enqueues multiple awaitable actions sequentially.
        /// The main goal is to batch insert logical procedures.
        /// It is used tightly linked with complex initialization scripts.
        /// </summary>
        void RegisterTasks(params Task[] tasks);

        /// <summary>
        /// Discards all pending and historical tasks.
        /// The main goal is to hard reset the queue state.
        /// It is used when resetting games or severing connections forcefully.
        /// </summary>
        void ClearTasks();
    }
}
