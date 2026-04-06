#nullable enable
using System;

namespace Scaffold.States
{
    /// <summary>
    /// Controls deferral and flushing for <see cref="DeferredStateEventHandler"/>. Not part of <see cref="IStateEventHandler"/> so subscription code stays narrow.
    /// </summary>
    public interface IStateEventDeferralController
    {
        /// <summary>
        /// Deliver all pending notifications to the inner handler immediately, regardless of deferral depth. Does not change the depth counter.
        /// </summary>
        void Flush();

        /// <summary>
        /// Increments deferral depth until disposed. Does not flush; pair with <see cref="Flush"/> to release buffered notifications.
        /// </summary>
        IDisposable BeginDeferScope();
    }
}
