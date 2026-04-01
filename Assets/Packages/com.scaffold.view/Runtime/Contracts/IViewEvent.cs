using UnityEngine;

namespace Scaffold.MVVM.Contracts
{
    public interface IViewEvent
    {
        Transform Source { get; }
        Transform Current { get; }
        bool IsConsumed { get; }

        void Consume();
        void Restore();
        void LogNext(Transform next);
    }
}

