using System;
using Scaffold.Maps;

namespace Scaffold.States
{
    /// <summary>
    /// Committed store snapshot (save/load) and pending overlay storage for mutator runs (same key shape: reference + state type).
    /// </summary>
    public class Snapshot : Map<IReference, Type, State>
    {
        public void Set(IReference reference, State state)
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
            if(TryGetValue(reference, typeof(TState), out var s))
            {
                return (TState)s;
            }
            return null;
        }
    }
}
