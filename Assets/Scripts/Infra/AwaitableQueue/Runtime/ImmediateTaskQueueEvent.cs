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
        private readonly List<Func<T, Awaitable>> _subscribers = new List<Func<T, Awaitable>>();
        private readonly Dictionary<Delegate, Func<T, Awaitable>> _wrapperMap = new Dictionary<Delegate, Func<T, Awaitable>>();

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
