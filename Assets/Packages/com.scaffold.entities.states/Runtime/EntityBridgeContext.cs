using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Scaffold.Entities;

using Store = Scaffold.States.Store;

namespace Scaffold.Entities.States
{
    internal sealed class EntityBridgeContext
    {
        private static readonly ConditionalWeakTable<Store, EntityBridgeContext> contextByStore = new();
        private static readonly object gate = new();

        private readonly Dictionary<InstanceId, IEntityDefinition> definitions = new();

        internal void Bind(InstanceId id, IEntityDefinition definition)
        {
            definitions[id] = definition;
        }

        internal bool TryGetDefinition(InstanceId id, out IEntityDefinition definition)
        {
            return definitions.TryGetValue(id, out definition);
        }

        internal static EntityBridgeContext CreateForStore(Store store)
        {
            if (contextByStore.TryGetValue(store, out var existing)) return existing;

            lock (gate)
            {
                if (contextByStore.TryGetValue(store, out existing)) return existing;

                var ctx = new EntityBridgeContext();
                store.RegisterMutator(new AddModifierMutator(ctx));
                store.RegisterMutator(new RemoveModifierMutator(ctx));
                store.RegisterMutator(new SetBaseValueMutator(ctx));
                store.RegisterMutator(new AddEntityVariableMutator(ctx));
                contextByStore.Add(store, ctx);
                return ctx;
            }
        }
    }
}
