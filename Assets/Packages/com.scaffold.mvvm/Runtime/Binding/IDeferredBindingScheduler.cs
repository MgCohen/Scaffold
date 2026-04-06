using System;

namespace Scaffold.MVVM.Binding
{
    /// <summary>
    /// Host-provided scheduling for deferred binding updates (Unity player loop, VContainer <c>ITickable</c>, tests, etc.).
    /// </summary>
    public interface IDeferredBindingScheduler
    {
        /// <summary>
        /// Queue <paramref name="continuation"/> to run once at the time implied by the binding policy and this implementation.
        /// </summary>
        void Schedule(Action continuation);
    }
}
