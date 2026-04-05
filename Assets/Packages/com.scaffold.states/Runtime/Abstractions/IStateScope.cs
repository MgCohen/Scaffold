#nullable enable

using System.Collections.Generic;

namespace Scaffold.States
{
    /// <summary>
    /// Read access to state slices for <see cref="Mutator{TState}.Change"/> / payload mutators.
    /// Implemented by <see cref="Store"/> (live slices) and the internal overlay scope used during registry mutator runs.
    /// </summary>
    public interface IStateScope
    {
        TState Get<TState>() where TState : BaseState;

        TState Get<TState>(IReference? reference) where TState : BaseState;

        IEnumerable<TState> GetAll<TState>() where TState : BaseState;
    }
}
