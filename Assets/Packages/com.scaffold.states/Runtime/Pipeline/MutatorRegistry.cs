#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Scaffold.States
{
    public sealed class MutatorRegistry
    {
        private readonly Dictionary<Type, List<IPayloadMutatorBinding>> registrations = new();

        public void Register<TState, TPayload>(Mutator<TState, TPayload> mutator) where TState : State
        {
            var key = typeof(TPayload);
            if (!registrations.TryGetValue(key, out List<IPayloadMutatorBinding>? list))
            {
                list = new List<IPayloadMutatorBinding>();
                registrations[key] = list;
            }

            ThrowIfDuplicateConcreteMutatorRegistered(list, mutator);
            list.Add(new RegisteredMutator<TState, TPayload>(mutator));
        }

        internal bool TryGet(Type payloadType, [NotNullWhen(true)] out IReadOnlyList<IPayloadMutatorBinding>? bindings)
        {
            if (registrations.TryGetValue(payloadType, out var list))
            {
                bindings = list;
                return true;
            }

            bindings = null;
            return false;
        }

        // Returns true when at least one mutator is registered for the given
        // payload type. Used by builders that need to fail fast on missing
        // registration before runtime execute attempts the dispatch.
        public bool IsRegistered(Type? payloadType)
        {
            if (payloadType == null) return false;
            return registrations.TryGetValue(payloadType, out var list) && list.Count > 0;
        }

        private void ThrowIfDuplicateConcreteMutatorRegistered<TState, TPayload>(List<IPayloadMutatorBinding> list, Mutator<TState, TPayload> mutator) where TState : State
        {
            Type mutatorType = mutator.GetType();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is RegisteredMutator<TState, TPayload> rm && rm.MutatorType == mutatorType)
                {
                    throw new DuplicateMutatorRegistrationException(
                        $"A mutator of type {mutatorType.FullName} is already registered for payload {typeof(TPayload).FullName}.");
                }
            }
        }
    }
}
