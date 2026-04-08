#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    public sealed class MutatorRegistry
    {
        private readonly Dictionary<Type, List<IPayloadMutatorBinding>> registrations = new();

        public void Register<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
        {
            var key = typeof(TPayload);
            if (!registrations.TryGetValue(key, out var list))
            {
                list = new List<IPayloadMutatorBinding>();
                registrations[key] = list;
            }

            list.Add(new RegisteredMutator<TState, TPayload>(mutator));
        }

        internal bool TryGet(Type payloadType, out IReadOnlyList<IPayloadMutatorBinding>? bindings)
        {
            if (registrations.TryGetValue(payloadType, out var list))
            {
                bindings = list;
                return true;
            }

            bindings = null;
            return false;
        }
    }
}
