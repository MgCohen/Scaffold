using System;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class EntityStateFactory
    {
        public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId) where TDefinition : IEntityDefinition
        {
            ValidateCreateArgs(definition, store, instanceId);
            store.RegisterSlice(instanceId, EntityVariableState.Empty);
            var provider = new EntityStateProvider<TDefinition>(instanceId, definition);
            store.RegisterAggregate(instanceId, provider);
            return store.Get<StateEntity<TDefinition>>(instanceId);
        }

        private static void ValidateCreateArgs<TDefinition>(TDefinition definition, Store store, InstanceId instanceId) where TDefinition : IEntityDefinition
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }
        }
    }
}
