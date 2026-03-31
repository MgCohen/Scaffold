using System;
using UnityEngine;

namespace Scaffold.MVVM.Contracts
{
    public interface IEventLedger
    {
        void Raise(Transform transform, IViewEvent evt);

        void Register(Transform transform, Action<IViewEvent> evt);

        void Unregister(Transform transform, Action<IViewEvent> evt);
    }
}


