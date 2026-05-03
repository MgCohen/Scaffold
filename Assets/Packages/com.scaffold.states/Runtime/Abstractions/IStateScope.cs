#nullable enable

using System.Collections.Generic;

namespace Scaffold.States
{
    public interface IStateScope
    {
        TState Get<TState>() where TState : BaseState;

        TState Get<TState>(IReference? reference) where TState : BaseState;

        bool TryGet<TState>(IReference? reference, out TState state) where TState : BaseState;

        IEnumerable<TState> GetAll<TState>() where TState : BaseState;
    }
}
