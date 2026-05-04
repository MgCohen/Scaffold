#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Scaffold.Maps;

namespace Scaffold.States
{
    public sealed class Snapshot : IEnumerable<KeyValuePair<Index<Reference, Type>, State>>
    {
        private readonly Map<Reference, Type, State> entries = new Map<Reference, Type, State>();

        public State this[Reference primary, Type secondary]
        {
            get => entries[primary, secondary];
            set => entries[primary, secondary] = value;
        }

        public int Count => entries.Count;

        public void Add(Reference primary, Type secondary, State value)
        {
            entries.Add(primary, secondary, value);
        }

        public bool Contains(Reference primary, Type secondary)
        {
            return entries.Contains(primary, secondary);
        }

        public bool TryGetValue(Reference primary, Type secondary, out State value)
        {
            return entries.TryGetValue(primary, secondary, out value);
        }

        public void Clear()
        {
            entries.Clear();
        }

        public IEnumerator<KeyValuePair<Index<Reference, Type>, State>> GetEnumerator()
        {
            return entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Set(Reference? reference, State state)
        {
            var r = reference ?? Reference.Null;
            var t = state.GetType();
            this[r, t] = state;
        }

        public TState Get<TState>() where TState : State
        {
            return Get<TState>(Reference.Null);
        }

        public TState Get<TState>(Reference reference) where TState : State
        {
            if (!TryGet<TState>(reference, out var state))
            {
                throw new KeyNotFoundException($"No snapshot entry for state type {typeof(TState).Name} at the given reference.");
            }

            return state;
        }

        public bool TryGet<TState>(Reference? reference, out TState state) where TState : State
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
