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
        /// <summary>
        /// Registers a callback to receive events.
        /// The main goal is to append asynchronous reactions to the broadcast cue.
        /// It is used by client domains initializing network dependencies.
        /// </summary>
        void Subscribe(Func<T, Awaitable> subscriber);

        /// <summary>
        /// Deregisters an existing callback from receiving events.
        /// The main goal is to clean up event listeners.
        /// It is used during destruction or state transitions.
        /// </summary>
        void Unsubscribe(Func<T, Awaitable> subscriber);
        /// <summary>
        /// Registers a strongly typed callback for a specific inheritor of T.
        /// The main goal is to filter events inherently limiting generic spam.
        /// It is used when an overarching handler dispatches multiple related event subclasses.
        /// </summary>
        void Subscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;

        /// <summary>
        /// Deregisters a strongly typed callback.
        /// The main goal is to safely remove filtered listeners.
        /// It is used contextually alongside derived subscribing.
        /// </summary>
        void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;
    }
}
