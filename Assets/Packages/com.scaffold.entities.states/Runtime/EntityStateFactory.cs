using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class EntityStateFactory
    {
        public static EntityInstance<TDefinition> Create<TDefinition>(TDefinition definition, Store store, Reference entityRef) where TDefinition : IEntityDefinition
        {
            store.RegisterSlice(entityRef, EntityState.Empty);
            return new EntityInstance<TDefinition>(definition, new StoreVariableStorage(store, entityRef));
        }
    }
}
