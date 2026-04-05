#nullable enable
using System.Collections.Generic;

namespace Scaffold.States
{
    /// <summary>
    /// Runs registered mutators against a <see cref="IStoreScratchpad"/> (overlay-first reads), then commits into the backing <see cref="Store"/>.
    /// Implements <see cref="IStateScope"/> by delegating to the scratchpad so payload bindings keep a stable <see cref="MutatorRunner"/> entry point.
    /// </summary>
    internal sealed class MutatorRunner : IStateScope
    {
        public MutatorRunner(IStoreScratchpad scratchpad)
        {
            this.scratchpad = scratchpad;
        }

        private readonly IStoreScratchpad scratchpad;

        internal void RunMutatorBindingsWithoutCommit(object payload, IReadOnlyList<IPayloadMutatorBinding> mutators, IReference executeReference)
        {
            foreach (var m in mutators)
            {
                m.Apply(payload, this, executeReference);
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
