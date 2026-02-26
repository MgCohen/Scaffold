using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    public class CompositeTaskQueueEvent<T>
    {
        private readonly TaskQueueEvent<T> _queuedEvent;
        private readonly ImmediateTaskQueueEvent<T> _immediateEvent;

        public CompositeTaskQueueEvent(ITaskQueueHandler queueHandler)
        {
            _queuedEvent = new TaskQueueEvent<T>(queueHandler);
            _immediateEvent = new ImmediateTaskQueueEvent<T>();
        }

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

        public async Awaitable InvokeAsync(T args)
        {
            _ = _immediateEvent.InvokeAsync(args);
            await Awaitable.NextFrameAsync();
            _queuedEvent.Invoke(args);
        }
    }
}
