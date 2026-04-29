using System;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class EntityStateFactory
    {
        public static StateEntity<TDefinition> Create<TDefinition>(TDefinition definition, Store store, InstanceId instanceId) where TDefinition : IEntityDefinition
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            var context = EntityBridgeContext.CreateForStore(store);
            store.RegisterSlice(instanceId, EntityVariableState.Empty);
            context.Bind(instanceId, definition);

            var storage = new StoreVariableStorage(store, instanceId, definition);
            var entity = new StateEntity<TDefinition>();
            entity.Setup(instanceId, definition, storage);
            return entity;
        }
    }
}
