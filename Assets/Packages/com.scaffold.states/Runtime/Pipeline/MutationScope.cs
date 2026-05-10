#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public sealed class MutationScope : IStateScope, IDisposable
    {
        internal MutationScope(MutatorRunner runner, MutatorRegistry registry, Action<MutatorRunner> returnRunner)
        {
            this.runner = runner;
            this.registry = registry;
            this.returnRunner = returnRunner;
        }

        private MutatorRunner? runner;
        private readonly MutatorRegistry registry;
        private readonly Action<MutatorRunner> returnRunner;
        private bool committed;

        public void Execute<TPayload>(TPayload payload)
        {
            Execute(null, payload);
        }

        public void Execute<TPayload>(Reference? reference, TPayload payload)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            MutatorRunner active = GetActiveRunner();
            Reference r = reference ?? Reference.Null;

            Type payloadType = payload.GetType();
            if (!registry.TryGet(payloadType, out var bindings) || bindings.Count == 0)
            {
                throw new MutatorNotRegisteredException(payloadType);
            }

            active.RunMutatorBindingsWithoutCommit(payload, bindings, r);
        }

        public void ExecuteMutator<TState>(Mutator<TState> mutator) where TState : State
        {
            ExecuteMutator(null, mutator);
        }

        public void ExecuteMutator<TState>(Reference? reference, Mutator<TState> mutator) where TState : State
        {
            if (mutator is null) throw new ArgumentNullException(nameof(mutator));
            GetActiveRunner().RunMutatorWithoutCommit(reference ?? Reference.Null, mutator);
        }

        public void ExecuteMutator<TState, TPayload>(Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            ExecuteMutator(null, mutator, payload);
        }

        public void ExecuteMutator<TState, TPayload>(Reference? reference, Mutator<TState, TPayload> mutator, TPayload payload) where TState : State
        {
            if (mutator is null) throw new ArgumentNullException(nameof(mutator));
            GetActiveRunner().RunTypedMutatorWithoutCommit(reference ?? Reference.Null, mutator, payload);
        }

        public void Commit()
        {
            MutatorRunner active = GetActiveRunner();
            committed = true;
            active.CommitOverlay();
        }

        public TState Get<TState>() where TState : BaseState
        {
            return GetActiveRunner().Get<TState>();
        }

        public TState Get<TState>(Reference? reference) where TState : BaseState
        {
            return GetActiveRunner().Get<TState>(reference);
        }

        public bool TryGet<TState>(Reference? reference, out TState state) where TState : BaseState
        {
            return GetActiveRunner().TryGet(reference, out state);
        }

        public IEnumerable<TState> GetAll<TState>() where TState : BaseState
        {
            return GetActiveRunner().GetAll<TState>();
        }

        public void Dispose()
        {
            if (runner is null) return;
            MutatorRunner r = runner;
            runner = null;
            returnRunner(r);
        }

        private MutatorRunner GetActiveRunner()
        {
            if (runner is null) throw new ObjectDisposedException(nameof(MutationScope));
            if (committed) throw new InvalidOperationException("MutationScope has already been committed.");
            return runner;
        }
    }
}
