#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.Pooling;

namespace Scaffold.States
{
    internal sealed class MutatorRunner : IStateScope, IPoolable
    {
        public MutatorRunner(IStoreScratchpad scratchpad)
        {
            this.scratchpad = scratchpad;
        }

        private readonly IStoreScratchpad scratchpad;

        event Action IPoolable.ReturnRequested
        {
            add { }
            remove { }
        }

        void IPoolable.OnTakenFromPool()
        {
        }

        void IPoolable.OnReturnedToPool()
        {
            scratchpad.Reset();
        }

        internal void RunMutatorBindingsWithoutCommit(object payload, IReadOnlyList<IPayloadMutatorBinding> mutators, IReference executeReference)
        {
            for (int i = 0; i < mutators.Count; i++)
            {
                mutators[i].Apply(payload, this, executeReference);
            }
        }

        internal void CommitOverlay()
        {
            scratchpad.Commit();
        }

        internal void RunMutatorWithoutCommit<TState>(IReference r, Mutator<TState> mutator) where TState : State
        {
            TState stateOut = mutator.Change(Get<TState>(r), this);
            scratchpad.SetPending(r, stateOut);
        }

        internal void RunTypedMutatorWithoutCommit<TState, TPayload>(IReference r, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            TState stateOut = mutator.Change(Get<TState>(r), payload, this);
            scratchpad.SetPending(r, stateOut);
        }

        public void SetPending<TState>(IReference? reference, TState state) where TState : State
        {
            scratchpad.SetPending(reference, state);
        }

        public IEnumerable<TState> GetAll<TState>() where TState : BaseState
        {
            return scratchpad.GetAll<TState>();
        }

        public TState Get<TState>() where TState : BaseState
        {
            return scratchpad.Get<TState>();
        }

        public TState Get<TState>(IReference? reference) where TState : BaseState
        {
            return scratchpad.Get<TState>(reference);
        }
    }
}
