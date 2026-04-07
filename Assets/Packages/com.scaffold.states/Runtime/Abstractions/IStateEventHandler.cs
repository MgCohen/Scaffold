#nullable enable
using System;

namespace Scaffold.States
{
    public interface IStateEventHandler
    {
        /// <summary>
        /// Notifies subscribers of a committed canonical or aggregate row change.
        /// </summary>
        /// <param name="reference">Slice reference (use <see cref="Reference.Null"/> when unkeyed).</param>
        /// <param name="state">Last committed state for the row; for <see cref="StateChangeEvent.Removed"/>, the final state before removal.</param>
        /// <param name="changeEvent">Whether the row was created, updated, or removed.</param>
        void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent);

        /// <summary>
        /// Equivalent to <see cref="Notify(IReference, BaseState, StateChangeEvent)"/> with <see cref="StateChangeEvent.Updated"/>.
        /// </summary>
        void Notify(IReference reference, BaseState state)
        {
            Notify(reference, state, StateChangeEvent.Updated);
        }

        void Subscribe<TState>(IReference reference, Action<IReference, TState, StateChangeEvent> action) where TState : BaseState;

        void SubscribeAllReferences<TState>(Action<IReference, TState, StateChangeEvent> action) where TState : BaseState;

        void SubscribeAny(Action<IReference, BaseState, StateChangeEvent> action);
    }
}
