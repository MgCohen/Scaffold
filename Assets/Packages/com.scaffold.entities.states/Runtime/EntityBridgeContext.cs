using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Scaffold.Entities;

using Store = Scaffold.States.Store;

namespace Scaffold.Entities.States
{
    /// <summary>
    /// Binds entity ids to definitions for mutators. Registers at most one mutator chain per payload type on each store
    /// (duplicate registration would apply the same logical change twice per execute).
    /// </summary>
    internal sealed class EntityBridgeContext
    {
        private static readonly ConditionalWeakTable<Store, EntityBridgeContext> ContextByStore = new();
        private static readonly object Gate = new();

        private readonly Dictionary<InstanceId, IEntityDefinition> definitions = new();

        internal static EntityBridgeContext GetOrAttach(Store store)
        {
            if (ContextByStore.TryGetValue(store, out var existing))
            {
                return existing;
            }

            lock (Gate)
            {
                if (ContextByStore.TryGetValue(store, out existing))
                {
                    return existing;
                }

                var ctx = new EntityBridgeContext();
                store.RegisterMutator(new AddModifierMutator(ctx));
                store.RegisterMutator(new RemoveModifierMutator(ctx));
                store.RegisterMutator(new SetBaseValueMutator(ctx));
                store.RegisterMutator(new AddEntityVariableMutator(ctx));
                ContextByStore.Add(store, ctx);
                return ctx;
            }
        }

        internal void Bind(InstanceId id, IEntityDefinition definition)
        {
            definitions[id] = definition;
        }

        internal bool TryGetDefinition(InstanceId id, out IEntityDefinition definition)
        {
            return definitions.TryGetValue(id, out definition);
        }
    }
}
