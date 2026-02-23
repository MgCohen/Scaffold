using System;

namespace Scaffold.Entities
{
    public class EntitiesService
    {
        private readonly EntityDefinitionLookup definitionLookup;
        private readonly EntityInstanceLookup instanceLookup;

        public EntitiesService() : this(new EntityDefinitionLookup(), new EntityInstanceLookup())
        {
        }

        public EntitiesService(EntityDefinitionLookup definitionLookup, EntityInstanceLookup instanceLookup)
        {
            ValidateLookup(definitionLookup, nameof(definitionLookup));
            ValidateLookup(instanceLookup, nameof(instanceLookup));
            this.definitionLookup = definitionLookup;
            this.instanceLookup = instanceLookup;
        }

        public void RegisterDefinition(IEntityDefinition definition)
        {
            definitionLookup.Register(definition);
        }

        public TDefinition GetDefinition<TDefinition>(EntityDefinitionId definitionId) where TDefinition : class, IEntityDefinition
        {
            TDefinition definition = definitionLookup.GetDefinition<TDefinition>(definitionId);
            return definition;
        }

        public void RegisterEntity(IEntityInstance entity)
        {
            instanceLookup.Register(entity);
        }

        public bool RemoveEntity(EntityInstanceId instanceId)
        {
            bool removed = instanceLookup.Remove(instanceId);
            return removed;
        }

        public TEntity GetEntity<TEntity>(EntityInstanceId instanceId, EntityDefinitionId definitionId) where TEntity : class, IEntityInstance
        {
            TEntity entity = instanceLookup.GetEntity<TEntity>(instanceId, definitionId);
            return entity;
        }

        public TEntity CreateEntity<TDefinition, TEntity>(EntityInstanceId instanceId, EntityDefinitionId definitionId) where TDefinition : class, IEntityDefinition where TEntity : class, IEntityInstance
        {
            TEntity entity = CreateEntity<TDefinition, TEntity>(instanceId, definitionId, null);
            return entity;
        }

        public TEntity CreateEntity<TDefinition, TEntity>(EntityInstanceId instanceId, EntityDefinitionId definitionId, Action<TEntity> initializeAction) where TDefinition : class, IEntityDefinition where TEntity : class, IEntityInstance
        {
            TDefinition definition = GetDefinition<TDefinition>(definitionId);
            IEntityInstance createdEntity = definition.CreateInstance(instanceId);
            TEntity typedEntity = ValidateCreatedEntity<TEntity>(createdEntity, definitionId);
            RunInitializer(initializeAction, typedEntity);
            RegisterEntity(typedEntity);
            return typedEntity;
        }

        private static void ValidateLookup(object lookup, string parameterName)
        {
            if (lookup == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        private static TEntity ValidateCreatedEntity<TEntity>(IEntityInstance createdEntity, EntityDefinitionId definitionId) where TEntity : class, IEntityInstance
        {
            ValidateCreatedEntityDefinition(createdEntity, definitionId);
            TEntity typedEntity = createdEntity as TEntity;
            if (typedEntity == null)
            {
                string message = $"Created entity '{createdEntity.GetType().Name}' cannot be cast to '{typeof(TEntity).Name}'.";
                throw new InvalidCastException(message);
            }
            return typedEntity;
        }

        private static void ValidateCreatedEntityDefinition(IEntityInstance createdEntity, EntityDefinitionId definitionId)
        {
            bool matches = createdEntity.DefinitionId == definitionId;
            if (!matches)
            {
                string message = $"Created entity id '{createdEntity.InstanceId.Value}' has definition '{createdEntity.DefinitionId.Value}' instead of '{definitionId.Value}'.";
                throw new InvalidOperationException(message);
            }
        }

        private static void RunInitializer<TEntity>(Action<TEntity> initializeAction, TEntity entity) where TEntity : class, IEntityInstance
        {
            bool hasInitializer = initializeAction != null;
            if (hasInitializer)
            {
                initializeAction(entity);
            }
        }
    }
}
