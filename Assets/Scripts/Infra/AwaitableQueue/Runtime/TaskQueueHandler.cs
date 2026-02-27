using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Executes registered awaitable tasks back-to-back safely.
    /// The main goal is to maintain internal lists of tasks and history and play them sequentially, pausing if required.
    /// It is used natively locally and remotely whenever data consistency across steps is structurally required.
    /// </summary>
    public class TaskQueueHandler : ITaskQueueHandler
    {
        private bool _enabled = true;

        public bool IsExecuting { get; private set; }

        private readonly Queue<Awaitable> _taskQueue = new Queue<Awaitable>();
        private readonly List<Awaitable> _taskHistory = new List<Awaitable>();

        public bool HasHistory
        {
            get { return _taskHistory.Count > 0; }
        }

        public event Action<Awaitable> OnTaskExecuting;

        private bool _isPaused;
        private AwaitableCompletionSource _pauseTask;

        public void Pause()
        {
            _isPaused = true;
            _pauseTask = new AwaitableCompletionSource();
        }

        public void Resume()
        {
            _isPaused = false;
            _pauseTask?.SetResult();
            _pauseTask = null;
        }

        public async Awaitable WaitForAllTasksToFinish()
        {
            while (IsExecuting || _taskQueue.Count > 0)
            {
                await Awaitable.NextFrameAsync();
            }
        }

        public void RegisterTask(Awaitable task)
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

        public void RegisterTasks(params Awaitable[] tasks)
        {
            foreach (Awaitable task in tasks)
            {
                RegisterTask(task);
            }
        }

        private async void ExecuteNextTaskAsync()
        {
            if (_taskQueue.Count <= 0)
            {
                return;
            }

            await WaitForResume();

            IsExecuting = true;
            Awaitable nextTask = _taskQueue.Dequeue();
            _taskHistory.Add(nextTask);

            OnTaskExecuting?.Invoke(nextTask);

            await nextTask;
            IsExecuting = false;

            ExecuteNextTaskAsync();
        }

        public void ClearTasks()
        {
            _taskQueue.Clear();
            _taskHistory.Clear();
        }

        private async Awaitable WaitForResume()
        {
            if (!_isPaused)
            {
                return;
            }
            await _pauseTask.Awaitable;
        }
    }
}
