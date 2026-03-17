using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.Utility.AwaitableQueue
{
    /// <summary>
    /// Executes registered awaitable tasks back-to-back safely.
    /// The main goal is to maintain internal lists of tasks and history and play them sequentially, pausing if required.
    /// It is used natively locally and remotely whenever data consistency across steps is structurally required.
    /// </summary>
    public class TaskQueueHandler : ITaskQueueHandler
    {
        /// <summary>
        /// Flag dictating if the queue accepts incoming tasks.
        /// The main goal is to toggle execution receptivity globally.
        /// It is used natively locally and remotely whenever data consistency across steps is structurally required.
        /// </summary>
        private bool _enabled = true;

        /// <summary>
        /// Gets whether the queue is currently processing an item.
        /// The main goal is to determine if the async loop is active.
        /// It is used by external scripts halting their logic based on queue progress.
        /// </summary>
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// The chronological queue of pending awaitable instructions.
        /// The main goal is to sequentially feed tasks.
        /// It is used constantly in the background loop to poll the next step.
        /// </summary>
        private readonly Queue<Task> _taskQueue = new Queue<Task>();

        /// <summary>
        /// The executed tasks preserved for historical reference.
        /// The main goal is to maintain a past state if queryable retrospection is needed.
        /// It is used lightly for debugging or replay functionalities.
        /// </summary>
        private readonly List<Task> _taskHistory = new List<Task>();

        /// <summary>
        /// Indicates if there are previously executed operations in history.
        /// The main goal is to query the handler's lifetime past log.
        /// It is used to determine if a stream of queues has run.
        /// </summary>
        public bool HasHistory
        {
            get { return _taskHistory.Count > 0; }
        }

        /// <summary>
        /// Event fired immediately before a task is executed.
        /// The main goal is to intercept and track in-flight actions.
        /// It is used by debuggers or visual indicators bridging system activity.
        /// </summary>
        public event Action<Task> OnTaskExecuting;

        /// <summary>
        /// Flag denoting whether the queue has been temporarily halted.
        /// The main goal is to pause the execution polling mechanism softly.
        /// It is used when asynchronous user input is injected between actions.
        /// </summary>
        private bool _isPaused;

        /// <summary>
        /// The awaiting completion block to suspend the queue tick.
        /// The main goal is to safely yield the async loop until resumption.
        /// It is used exclusively inside the wait-for-resume function.
        /// </summary>
        private TaskCompletionSource<bool> _pauseTask;

        /// <summary>
        /// Halts the sequential execution of pending tasks.
        /// The main goal is to freeze operations safely between events.
        /// It is used when the system requires user input or forced yields.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
            _pauseTask = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// Resumes execution of halted tasks in the queue.
        /// The main goal is to restore the operational sequence.
        /// It is used globally to unblock halted handlers.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            _pauseTask?.SetResult(true);
            _pauseTask = null;
        }

        /// <summary>
        /// Asynchronously blocks until the interior queue is drained.
        /// The main goal is to ensure dependencies finish completely.
        /// It is used gracefully in tear-down scripts.
        /// </summary>
        public async Task WaitForAllTasksToFinish()
        {
            while (IsExecuting || _taskQueue.Count > 0)
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Enqueues a single awaitable action.
        /// The main goal is to safely pipe the execution step without threading errors.
        /// It is used continually when systems append background tasks.
        /// </summary>
        public void RegisterTask(Task task)
        {
            if (!_enabled)
            {
                return;
            }

            _taskQueue.Enqueue(task);
            if (!IsExecuting)
            {
                ExecuteNextTaskAsync();
            }
        }

        /// <summary>
        /// Enqueues multiple awaitable actions sequentially.
        /// The main goal is to batch insert logical procedures.
        /// It is used tightly linked with complex initialization scripts.
        /// </summary>
        public void RegisterTasks(params Task[] tasks)
        {
            foreach (Task task in tasks)
            {
                RegisterTask(task);
            }
        }

        /// <summary>
        /// Unwinds the queue taking one task at a time securely.
        /// The main goal is to operate the heartbeat of the handler asynchronously.
        /// It is used recursively resolving its completion step onto itself.
        /// </summary>
        private async void ExecuteNextTaskAsync()
        {
            if (_taskQueue.Count <= 0)
            {
                return;
            }

            await WaitForResume();

            IsExecuting = true;
            Task nextTask = _taskQueue.Dequeue();
            _taskHistory.Add(nextTask);

            OnTaskExecuting?.Invoke(nextTask);

            await nextTask;
            IsExecuting = false;

            ExecuteNextTaskAsync();
        }

        /// <summary>
        /// Discards all pending and historical tasks.
        /// The main goal is to hard reset the queue state.
        /// It is used when resetting games or severing connections forcefully.
        /// </summary>
        public void ClearTasks()
        {
            _taskQueue.Clear();
            _taskHistory.Clear();
        }

        /// <summary>
        /// Suspend iteration if pause state applies.
        /// The main goal is to yield CPU frames dynamically upon request.
        /// It is used before picking up novel queue entries.
        /// </summary>
        private async Task WaitForResume()
        {
            if (!_isPaused)
            {
                return;
            }
            await _pauseTask.Task;
        }
    }
}
