using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
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
