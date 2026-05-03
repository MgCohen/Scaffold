#nullable enable
using System;
using Scaffold.Maps;

namespace Scaffold.States
{
    public class Snapshot : Map<IReference, Type, State>
    {
        public void Set(IReference? reference, State state)
        {
            var r = reference ?? Reference.Null;
            var t = state.GetType();
            this[r, t] = state;
        }

        public TState Get<TState>() where TState : State
        {
            return Get<TState>(Reference.Null);
        }

        public TState Get<TState>(IReference reference) where TState : State
        {
            if (!TryGet<TState>(reference, out var state))
            {
                throw new KeyNotFoundException($"No snapshot entry for state type {typeof(TState).Name} at the given reference.");
            }

            return state;
        }

        public bool TryGet<TState>(IReference? reference, out TState state) where TState : State
        {
            var r = reference ?? Reference.Null;
            if (TryGetValue(r, typeof(TState), out var s))
            {
                state = (TState)s;
                return true;
            }

            state = default!;
            return false;
        }
    }
}
