using System;
using UnityEngine;

namespace Scaffold.AwaitableQueue
{
    public interface ITaskQueueEvent<T>
    {
        void Subscribe(Func<T, Awaitable> subscriber);
        void Unsubscribe(Func<T, Awaitable> subscriber);
        void Subscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;
        void Unsubscribe<TDerived>(Func<TDerived, Awaitable> subscriber) where TDerived : T;
    }
}
