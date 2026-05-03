#nullable enable

using System;

namespace Scaffold.States
{
    internal sealed class RegisteredMutator<TState, TPayload> : IPayloadMutatorBinding where TState : State
    {
        internal RegisteredMutator(Mutator<TState, TPayload> mutator)
        {
            this.mutator = mutator;
        }

        internal Type MutatorType => mutator.GetType();

        private readonly Mutator<TState, TPayload> mutator;

        public void Apply(object payload, MutatorRunner runner, IReference executeReference)
        {
            var p = (TPayload)payload;
            IReference r = FromPayload(payload, executeReference);
            TState stateIn = runner.Get<TState>(r);
            TState stateOut = mutator.Change(stateIn, p, runner);
            runner.SetPending(r, stateOut);
        }

        private static IReference FromPayload(object payload, IReference executeReference)
        {
            if (!ReferenceEquals(executeReference, Reference.Null))
            {
                return executeReference;
            }

            if (payload is IPayloadReference pr)
            {
                return pr.GetReference();
            }

            return Reference.Null;
        }
    }
}
