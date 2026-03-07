using System;
using UnityEngine;

namespace Scaffold.MVVM
{
    public interface IEventLedger
    {
        void Raise(Transform transform, ViewEvent evt);

        void Register(Transform transform, Action<ViewEvent> evt);

        void Unregister(Transform transform, Action<ViewEvent> evt);
    }
}
