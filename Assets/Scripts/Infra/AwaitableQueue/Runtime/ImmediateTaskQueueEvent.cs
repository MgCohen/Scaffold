using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Implements an event handler that triggers asynchronous subscriber actions instantly without queueing.
    /// The main goal is to loop over subscribers and begin their execution routines in-place on the same frame.
    /// It is used for real-time reactivity when event delivery has a higher priority than orderly processing.
    /// </summary>
    public class ImmediateTaskQueueEvent<T> : ITaskQueueEvent<T>
    {
        /// <summary>
        /// The list of delegates dynamically tethered locally.
        /// The main goal is to keep execution states for immediate loops.
        /// It is used purely for iterating standard delegates directly.
        /// </summary>
        private readonly List<Func<T, Awaitable>> _subscribers = new List<Func<T, Awaitable>>();

        /// <summary>
        /// Relates raw typed callbacks to their dynamically created wrapper invocations.
        /// The main goal is to ensure dereferencing unsubscribes cleanly for typed hooks.
        /// It is used primarily mapping strongly-typed responses immediately.
        /// </summary>
        private readonly Dictionary<Delegate, Func<T, Awaitable>> _wrapperMap = new Dictionary<Delegate, Func<T, Awaitable>>();

        /// <summary>
        /// Registers a callback to receive events immediately.
        /// The main goal is to append asynchronous reactions to the broadcast cue synchronously.
        /// It is used by local systems requiring high priority reaction.
        /// </summary>
        public void Subscribe(Func<T, Awaitable> subscriber)
        {
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        /// <summary>
        /// Deregisters an existing callback from receiving events immediately.
        /// The main goal is to clean up event listeners safely.
        /// It is used during destruction or state transitions prioritizing garbage collection.
        /// </summary>
        public void Unsubscribe(Func<T, Awaitable> subscriber)
        {
            _subscribers.Remove(subscriber);
        }

        /// <summary>
        /// Registers a strongly typed callback for a specific inheritor of T immediately.
        /// The main goal is to filter events inherently limiting generic spam upon reception.
        /// It is used when an overarching handler dispatches multiple related event subclasses seamlessly.
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
        /// Deregisters a strongly typed callback cleanly.
        /// The main goal is to safely remove filtered listeners out of the map index.
        /// It is used contextually alongside derived subscribing cleanups.
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
        /// Invokes the parameter arguments dynamically over all immediate listener elements without queueing.
        /// The main goal is to aggressively trigger functions back to back instantly.
        /// It is used by latency-critical callbacks unburdened by orderly frame wait boundaries.
        /// </summary>
        public async Awaitable InvokeAsync(T args)
        {
            foreach (Func<T, Awaitable> subscriber in _subscribers)
            {
                _ = subscriber(args);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
