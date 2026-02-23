using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public class EntityInstanceLookup
    {
        private readonly Dictionary<EntityInstanceId, IEntityInstance> instancesById;
        private readonly Dictionary<Type, Dictionary<EntityInstanceId, IEntityInstance>> instancesByType;
        private readonly Dictionary<EntityDefinitionId, HashSet<EntityInstanceId>> instancesByDefinition;

        public EntityInstanceLookup()
        {
            instancesById = new Dictionary<EntityInstanceId, IEntityInstance>();
            instancesByType = new Dictionary<Type, Dictionary<EntityInstanceId, IEntityInstance>>();
            instancesByDefinition = new Dictionary<EntityDefinitionId, HashSet<EntityInstanceId>>();
        }

        public void Register(IEntityInstance entity)
        {
            ValidateEntity(entity);
            AddEntityById(entity);
            AddEntityByType(entity);
            AddEntityByDefinition(entity);
        }

        public bool Remove(EntityInstanceId instanceId)
        {
            bool removed = false;
            bool found = instancesById.TryGetValue(instanceId, out IEntityInstance entity);
            if (found)
            {
                RemoveEntityById(instanceId);
                RemoveEntityByType(entity);
                RemoveEntityByDefinition(entity);
                removed = true;
            }
            return removed;
        }

        public TEntity GetEntity<TEntity>(EntityInstanceId instanceId, EntityDefinitionId definitionId) where TEntity : class, IEntityInstance
        {
            IEntityInstance entity = GetEntity(instanceId);
            Type entityType = typeof(TEntity);
            ValidateDefinitionId(entity, definitionId, entityType);
            ValidateEntityType(entity, entityType);
            TEntity typedEntity = CastEntity<TEntity>(entity, entityType);
            return typedEntity;
        }

        public IEntityInstance GetEntity(EntityInstanceId instanceId)
        {
            bool found = instancesById.TryGetValue(instanceId, out IEntityInstance entity);
            if (!found)
            {
                string message = $"Entity instance '{instanceId.Value}' was not found.";
                throw new KeyNotFoundException(message);
            }
            return entity;
        }

        public IReadOnlyCollection<EntityInstanceId> GetEntityIds(EntityDefinitionId definitionId)
        {
            IReadOnlyCollection<EntityInstanceId> ids = Array.Empty<EntityInstanceId>();
            bool found = instancesByDefinition.TryGetValue(definitionId, out HashSet<EntityInstanceId> entityIds);
            if (found)
            {
                ids = new List<EntityInstanceId>(entityIds);
            }
            return ids;
        }

        private void ValidateEntity(IEntityInstance entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
        }

        private void AddEntityById(IEntityInstance entity)
        {
            bool exists = instancesById.ContainsKey(entity.InstanceId);
            if (exists)
            {
                string message = $"Entity instance id '{entity.InstanceId.Value}' is already registered.";
                throw new InvalidOperationException(message);
            }
            instancesById.Add(entity.InstanceId, entity);
        }

        private void AddEntityByType(IEntityInstance entity)
        {
            Type entityType = entity.GetType();
            Dictionary<EntityInstanceId, IEntityInstance> entityBucket = GetOrCreateEntityBucket(entityType);
            entityBucket.Add(entity.InstanceId, entity);
        }

        private void AddEntityByDefinition(IEntityInstance entity)
        {
            HashSet<EntityInstanceId> entityIds = GetOrCreateDefinitionSet(entity.DefinitionId);
            entityIds.Add(entity.InstanceId);
        }

        private void RemoveEntityById(EntityInstanceId instanceId)
        {
            instancesById.Remove(instanceId);
        }

        private void RemoveEntityByType(IEntityInstance entity)
        {
            Type entityType = entity.GetType();
            bool hasBucket = instancesByType.TryGetValue(entityType, out Dictionary<EntityInstanceId, IEntityInstance> entityBucket);
            if (hasBucket)
            {
                entityBucket.Remove(entity.InstanceId);
            }
        }

        private void RemoveEntityByDefinition(IEntityInstance entity)
        {
            bool hasSet = instancesByDefinition.TryGetValue(entity.DefinitionId, out HashSet<EntityInstanceId> entityIds);
            if (hasSet)
            {
                entityIds.Remove(entity.InstanceId);
            }
        }

        private Dictionary<EntityInstanceId, IEntityInstance> GetOrCreateEntityBucket(Type entityType)
        {
            bool hasBucket = instancesByType.TryGetValue(entityType, out Dictionary<EntityInstanceId, IEntityInstance> entityBucket);
            if (!hasBucket)
            {
                entityBucket = new Dictionary<EntityInstanceId, IEntityInstance>();
                instancesByType.Add(entityType, entityBucket);
            }
            return entityBucket;
        }

        private HashSet<EntityInstanceId> GetOrCreateDefinitionSet(EntityDefinitionId definitionId)
        {
            bool hasSet = instancesByDefinition.TryGetValue(definitionId, out HashSet<EntityInstanceId> entityIds);
            if (!hasSet)
            {
                entityIds = new HashSet<EntityInstanceId>();
                instancesByDefinition.Add(definitionId, entityIds);
            }
            return entityIds;
        }

        private void ValidateEntityType(IEntityInstance entity, Type entityType)
        {
            bool matches = entityType.IsInstanceOfType(entity);
            if (!matches)
            {
                string message = $"Entity id '{entity.InstanceId.Value}' is '{entity.GetType().Name}' and cannot be used as '{entityType.Name}'.";
                throw new InvalidCastException(message);
            }
        }

        private void ValidateDefinitionId(IEntityInstance entity, EntityDefinitionId definitionId, Type entityType)
        {
            bool matches = entity.DefinitionId == definitionId;
            if (!matches)
            {
                string message = $"Entity type '{entityType.Name}' id '{entity.InstanceId.Value}' has definition '{entity.DefinitionId.Value}' instead of '{definitionId.Value}'.";
                throw new InvalidOperationException(message);
            }
        }

        private TEntity CastEntity<TEntity>(IEntityInstance entity, Type entityType) where TEntity : class, IEntityInstance
        {
            TEntity typedEntity = entity as TEntity;
            if (typedEntity == null)
            {
                string message = $"Entity '{entityType.Name}' id '{entity.InstanceId.Value}' cannot be cast to '{typeof(TEntity).Name}'.";
                throw new InvalidCastException(message);
            }
            return typedEntity;
        }
    }
}
