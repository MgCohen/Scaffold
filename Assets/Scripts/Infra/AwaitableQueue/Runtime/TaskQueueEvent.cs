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
        /// <summary>
        /// The associated handler coordinating serial executions sequentially.
        /// The main goal is to store tasks globally allowing uniform awaiting.
        /// It is used as a proxy execution pipeline.
        /// </summary>
        private readonly ITaskQueueHandler _queueHandler;

        /// <summary>
        /// The list of delegates dynamically tethered to the queue.
        /// The main goal is to keep execution states for generic invoke cycles.
        /// It is used purely for iterating standard delegates.
        /// </summary>
        private readonly List<Func<T, Awaitable>> _subscribers = new List<Func<T, Awaitable>>();

        /// <summary>
        /// Relates raw typed callbacks to their dynamically created wrapper invocations.
        /// The main goal is to ensure dereferencing unsubscribes cleanly for typed hooks.
        /// It is used primarily mapping strongly-typed responses.
        /// </summary>
        private readonly Dictionary<Delegate, Func<T, Awaitable>> _wrapperMap = new Dictionary<Delegate, Func<T, Awaitable>>();

        /// <summary>
        /// Creates a new TaskQueueEvent binding exclusively to a handler instance.
        /// The main goal is to isolate execution streams within a context block.
        /// It is used heavily by modular system components.
        /// </summary>
        public TaskQueueEvent(ITaskQueueHandler queueHandler)
        {
            _queueHandler = queueHandler;
        }

        /// <summary>
        /// Safely attaches a listener if the list is unique.
        /// The main goal is to pipe future calls towards to the subscriber callback.
        /// It is used commonly when controllers init network boundaries.
        /// </summary>
        public void Subscribe(Func<T, Awaitable> subscriber)
        {
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// Disconnects a listener fully.
        /// The main goal is to untether the handler stopping side effects.
        /// It is used to destroy components elegantly across module bounds.
        /// </summary>
        public void Unsubscribe(Func<T, Awaitable> subscriber)
        {
            _subscribers.Remove(subscriber);
        }

        /// <summary>
        /// Dynamically registers a mapped type wrapper restricting callback filters securely.
        /// The main goal is to inject polymorphism over raw delegate queues.
        /// It is used continuously while evaluating raw JSON networking interfaces.
        /// </summary>
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

        /// <summary>
        /// Looks up and deletes mapped wrapped listener cleanly.
        /// The main goal is to clear out filtered mappings without referencing anonymous wrapper signatures directly.
        /// It is used when a dynamically subscribed module tier finishes duty.
        /// </summary>
        public void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T
        {
            if (_wrapperMap.TryGetValue(subscriber, out Func<T, Awaitable> wrapper))
            {
                _subscribers.Remove(wrapper);
                _wrapperMap.Remove(subscriber);
            }
        }

        /// <summary>
        /// Broadcasts an event parameter sequentially iterating over internal callbacks.
        /// The main goal is to pipeline every delegate invoke to the overarching proxy queue handler correctly.
        /// It is used uniformly inside CloudModule logic steps.
        /// </summary>
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
