using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Combines immediate and queued event invocation behaviors into a single wrapper.
    /// The main goal is to allow subscribers to choose between receiving events immediately or enqueued securely.
    /// It is used natively by services like Cloud Code when broad response distribution needs flexible timing.
    /// </summary>
    public class CompositeTaskQueueEvent<T>
    {
        /// <summary>
        /// The sequential queued event backing field.
        /// The main goal is to provide scheduled callback execution via the task queue handler.
        /// It is used to encapsulate non-immediate invocation logic securely.
        /// </summary>
        private readonly TaskQueueEvent<T> _queuedEvent;

        /// <summary>
        /// The synchronous immediate event backing field.
        /// The main goal is to provide instantaneous execution bypassing the queue.
        /// It is used to ensure urgent scripts respond to broadcasts within the same frame.
        /// </summary>
        private readonly ImmediateTaskQueueEvent<T> _immediateEvent;

        /// <summary>
        /// Bootstraps the composite event struct providing a handler.
        /// The main goal is to unify sequential and immediate execution paths.
        /// It is used implicitly when exposing game-wide dispatchers.
        /// </summary>
        public CompositeTaskQueueEvent(ITaskQueueHandler queueHandler)
        {
            _queuedEvent = new TaskQueueEvent<T>(queueHandler);
            _immediateEvent = new ImmediateTaskQueueEvent<T>();
        }

        /// <summary>
        /// Subscribes the callback either synchronously or via the queue.
        /// The main goal is to expose granular flexibility to downstream consumers.
        /// It is used by gameplay scripts choosing real-time execution priority.
        /// </summary>
        public void Subscribe(Func<T, Awaitable> subscriber, bool immediate)
        {
            if (immediate)
            {
                _immediateEvent.Subscribe(subscriber);
            }
            else
            {
                _queuedEvent.Subscribe(subscriber);
            }
        }

        /// <summary>
        /// Unsubscribes the callback from the target event layer.
        /// The main goal is to securely route the cleanup internally.
        /// It is used uniformly independent of priority context.
        /// </summary>
        public void Unsubscribe(Func<T, Awaitable> subscriber, bool immediate)
        {
            if (immediate)
            {
                _immediateEvent.Unsubscribe(subscriber);
            }
            else
            {
                _queuedEvent.Unsubscribe(subscriber);
            }
        }

        /// <summary>
        /// Subscribes a typed callback selectively bypassing general iterations.
        /// The main goal is to route exact types automatically into immediate or queued lists.
        /// It is used heavily when managing broad polymorphic data.
        /// </summary>
        public void Subscribe<TDerived>(Func<TDerived, Awaitable> subscriber, bool immediate) where TDerived : T
        {
            if (immediate)
            {
                _immediateEvent.Subscribe<TDerived>(subscriber);
            }
            else
            {
                _queuedEvent.Subscribe<TDerived>(subscriber);
            }
        }

        /// <summary>
        /// Unsubscribes a typed callback completely from the selected layer.
        /// The main goal is to tear down dynamic polymorphic mappings cleanly.
        /// It is used during un-injection sequences.
        /// </summary>
        public void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber, bool immediate) where TDerived : T
        {
            if (immediate)
            {
                _immediateEvent.Unsubscribe<TDerived>(subscriber);
            }
            else
            {
                _queuedEvent.Unsubscribe<TDerived>(subscriber);
            }
        }

        /// <summary>
        /// Dispatches the generic parameter to both event networks asynchronously.
        /// The main goal is to resolve immediate subscribers precisely before firing queued scripts.
        /// It is used as the absolute execution origin of remote callbacks.
        /// </summary>
        public async Awaitable InvokeAsync(T args)
        {
            _ = _immediateEvent.InvokeAsync(args);
            await Awaitable.NextFrameAsync();
            _queuedEvent.Invoke(args);
        }
    }
}
