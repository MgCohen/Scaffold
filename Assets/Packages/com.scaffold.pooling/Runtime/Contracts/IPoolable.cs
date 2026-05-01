using System;

namespace Scaffold.Pooling
{
    public interface IPoolable
    {
        void OnTakenFromPool();

        void OnReturnedToPool();

        event Action ReturnRequested;
    }
}
