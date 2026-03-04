using Scaffold.Maps;
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public class Store
    {
        public Store(IStateEventHandler eventHandler, params Slice[] slices)
        {
            this.eventHandler = eventHandler;
            this.map = new Map<IReference, Type, Slice>();
            foreach (var slice in slices)
            {
                map.Add(slice.Reference, slice.State.GetType(), slice);
            }
        }

        private Map<IReference, Type, Slice> map;
        private IStateEventHandler eventHandler;

        #region Mutators
        public void Execute<TState>(Mutator<TState> mutator) where TState : State
        {
            Execute<TState>(Reference.Null, mutator);
        }

        public void Execute<TState>(IReference reference, Mutator<TState> mutator) where TState : State
        {
            TState state = Get<TState>(reference);
            state = mutator.Change(state);
            Set(reference, state);
        }

        private void Set(IReference reference, State state)
        {
            Slice slice = GetSlice(reference, state.GetType());
            slice.Set(state);
            eventHandler.Notify(reference, state);
        }
        #endregion

        #region Subscriptions
        public void Subscribe<TState>(Action<IReference, TState> action) where TState : State
        {
            Subscribe<TState>(Reference.Null, action);
        }

        public void Subscribe<TState>(IReference reference, Action<IReference, TState> action) where TState : State
        {
            eventHandler.Subscribe<TState>(reference, action);
        }
        #endregion

        #region Getters
        public TState Get<TState>() where TState : State
        {
            return Get<TState>(Reference.Null);
        }

        public TState Get<TState>(IReference reference) where TState : State
        {
            Slice slice = GetSlice(reference, typeof(TState));
            return slice.State as TState;
        }

        private Slice GetSlice(IReference reference, Type type)
        {
            return map[reference, type];
        }
        #endregion

        #region Snapshots
        public Snapshot SaveSnapshot()
        {
            Snapshot snapshot = new Snapshot();
            foreach(var entry in map)
            {
                snapshot.Add(entry.Key.primary, entry.Key.secondary, entry.Value.State);
            }
            return snapshot;
        }

        public void LoadSnapshot(Snapshot snapshot)
        {
            foreach (var entry in snapshot)
            {
                Set(entry.Key.primary, entry.Value);
            }
        }
        #endregion
    }
}
