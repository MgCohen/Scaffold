#nullable enable

namespace Scaffold.States
{
    internal sealed class RegisteredMutator<TState, TPayload> : IPayloadMutatorBinding where TState : State
    {
        internal RegisteredMutator(IReference reference, Mutator<TState, TPayload> mutator)
        {
            this.reference = reference;
            this.mutator = mutator;
        }

        private readonly IReference reference;
        private readonly Mutator<TState, TPayload> mutator;

        public void Apply(object payload, MutatorRunner runner, IReference executeReference)
        {
            var p = (TPayload)payload;
            IReference r = ResolveReference(payload, executeReference);
            TState stateIn = runner.Get<TState>(r);
            TState stateOut = mutator.Change(stateIn, p, runner);
            runner.SetPending(r, stateOut);
        }

        private IReference ResolveReference(object payload, IReference executeReference)
        {
            if (!executeReference.Equals(Reference.Null))
            {
                return executeReference;
            }

            if (reference.Equals(Reference.Null) && payload is IPayloadReference pr)
            {
                return pr.GetReference();
            }

            return reference;
        }
    }
}
