using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    /// <summary>
    /// Represents an awaitable event that supports subscribing and unsubscribing asynchronous callbacks.
    /// The main goal is to standardize the event signature for multi-subscriber task queues.
    /// It is used across modules to handle decoupled event broadcasting where listeners perform asynchronous work.
    /// </summary>
    public interface ITaskQueueEvent<T>
    {
        void Subscribe(Func<T, Awaitable> subscriber);
        void Unsubscribe(Func<T, Awaitable> subscriber);
        void Subscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;
        void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;
    }
}
