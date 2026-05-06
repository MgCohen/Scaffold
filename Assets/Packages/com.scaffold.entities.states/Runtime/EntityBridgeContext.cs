#nullable enable
using System;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class EntityBridgeContext
    {
        public static void RegisterMutators(StoreBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.UseMutatorDispatcher(new GeneratedMutatorDispatcher());
            builder.RegisterMutator(new AddModifierMutator());
            builder.RegisterMutator(new RemoveModifierMutator());
            builder.RegisterMutator(new SetBaseValueMutator());
            builder.RegisterMutator(new AddEntityVariableMutator());
            builder.RegisterMutator(new RemoveEntityVariableMutator());
            builder.RegisterMutator(new RemoveModifiersBySourceMutator());
            builder.RegisterMutator(new ClearModifiersMutator());
        }
    }
}
