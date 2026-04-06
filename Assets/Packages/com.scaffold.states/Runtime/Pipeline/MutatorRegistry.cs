#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.States
{
    /// <summary>
    /// Maps payload CLR types to ordered <see cref="Mutator{TState, TPayload}"/> instances.
    /// Each <see cref="Store.Execute{TPayload}(TPayload)"/> run applies every registered mutator for that payload in order, using one overlay commit.
    /// </summary>
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
