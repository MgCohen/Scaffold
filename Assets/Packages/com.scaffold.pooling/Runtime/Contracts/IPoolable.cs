using System;

namespace Scaffold.Pooling
{
    /// <summary>
    /// Optional lifecycle hooks for instances managed by <see cref="Pool{T}"/>.
    /// </summary>
    public interface IPoolable
    {
        void OnTakenFromPool();

        void OnReturnedToPool();

        /// <summary>
        /// Raised by the instance when it should be returned to the pool.
        /// The pool subscribes on <see cref="Pool{T}.Take"/> and unsubscribes on return.
        /// </summary>
        event Action ReturnRequested;
    }
}
