using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Implements an event handler that registers subscriber actions onto a managed queue handler.
    /// The main goal is to ensure all event reactions are pipelined sequentially without causing race conditions.
    /// It is used closely with the TaskQueueHandler to handle heavy event payloads systematically.
    /// </summary>
    public class TaskQueueEvent<T> : ITaskQueueEvent<T>
    {
        private readonly ITaskQueueHandler _queueHandler;
        private readonly List<Func<T, Awaitable>> _subscribers = new List<Func<T, Awaitable>>();
        private readonly Dictionary<Delegate, Func<T, Awaitable>> _wrapperMap = new Dictionary<Delegate, Func<T, Awaitable>>();

        public TaskQueueEvent(ITaskQueueHandler queueHandler)
        {
            _queueHandler = queueHandler;
        }

        public void Subscribe(Func<T, Awaitable> subscriber)
        {
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        public void Unsubscribe(Func<T, Awaitable> subscriber)
        {
            _subscribers.Remove(subscriber);
        }

        public void Subscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T
        {
            if (_wrapperMap.ContainsKey(subscriber)) return;

            Func<T, Awaitable> wrapper = args =>
            {
                if (args is TDerived derivedArgs)
                {
                    return subscriber(derivedArgs);
                }
                var completionSource = new AwaitableCompletionSource();
                completionSource.SetResult();
                return completionSource.Awaitable;
            };

            _wrapperMap[subscriber] = wrapper;
            _subscribers.Add(wrapper);
        }

        public void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T
        {
            if (_wrapperMap.TryGetValue(subscriber, out Func<T, Awaitable> wrapper))
            {
                _subscribers.Remove(wrapper);
                _wrapperMap.Remove(subscriber);
            }
        }

        public void Invoke(T args)
        {
            foreach (Func<T, Awaitable> subscriber in _subscribers)
            {
                Awaitable task = subscriber(args);
                _queueHandler.RegisterTask(task);
            }
        }
    }
}
