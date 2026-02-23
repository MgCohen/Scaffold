using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public class EntityDefinitionLookup
    {
        private readonly Dictionary<EntityDefinitionId, IEntityDefinition> definitionsById;
        private readonly Dictionary<Type, Dictionary<EntityDefinitionId, IEntityDefinition>> definitionsByType;

        public EntityDefinitionLookup()
        {
            definitionsById = new Dictionary<EntityDefinitionId, IEntityDefinition>();
            definitionsByType = new Dictionary<Type, Dictionary<EntityDefinitionId, IEntityDefinition>>();
        }

        public void Register(IEntityDefinition definition)
        {
            ValidateDefinition(definition);
            AddDefinitionById(definition);
            AddDefinitionByType(definition);
        }

        public TDefinition GetDefinition<TDefinition>(EntityDefinitionId definitionId) where TDefinition : class, IEntityDefinition
        {
            IEntityDefinition definition = GetDefinition(definitionId);
            Type definitionType = typeof(TDefinition);
            ValidateDefinitionType(definition, definitionType);
            TDefinition typedDefinition = CastDefinition<TDefinition>(definition, definitionType);
            return typedDefinition;
        }

        public IEntityDefinition GetDefinition(EntityDefinitionId definitionId)
        {
            bool found = definitionsById.TryGetValue(definitionId, out IEntityDefinition definition);
            if (!found)
            {
                string message = $"Definition '{definitionId.Value}' was not found.";
                throw new KeyNotFoundException(message);
            }
            return definition;
        }

        private void ValidateDefinition(IEntityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
        }

        private void AddDefinitionById(IEntityDefinition definition)
        {
            bool exists = definitionsById.ContainsKey(definition.DefinitionId);
            if (exists)
            {
                string message = $"Definition id '{definition.DefinitionId.Value}' is already registered.";
                throw new InvalidOperationException(message);
            }
            definitionsById.Add(definition.DefinitionId, definition);
        }

        private void AddDefinitionByType(IEntityDefinition definition)
        {
            Dictionary<EntityDefinitionId, IEntityDefinition> definitionBucket = GetOrCreateDefinitionBucket(definition.DefinitionType);
            bool exists = definitionBucket.ContainsKey(definition.DefinitionId);
            if (exists)
            {
                string message = $"Definition '{definition.DefinitionType.Name}' with id '{definition.DefinitionId.Value}' is already registered.";
                throw new InvalidOperationException(message);
            }
            definitionBucket.Add(definition.DefinitionId, definition);
        }

        private Dictionary<EntityDefinitionId, IEntityDefinition> GetOrCreateDefinitionBucket(Type definitionType)
        {
            bool hasBucket = definitionsByType.TryGetValue(definitionType, out Dictionary<EntityDefinitionId, IEntityDefinition> definitionBucket);
            if (!hasBucket)
            {
                definitionBucket = new Dictionary<EntityDefinitionId, IEntityDefinition>();
                definitionsByType.Add(definitionType, definitionBucket);
            }
            return definitionBucket;
        }

        private void ValidateDefinitionType(IEntityDefinition definition, Type definitionType)
        {
            bool matches = definitionType.IsInstanceOfType(definition);
            if (!matches)
            {
                string message = $"Definition id '{definition.DefinitionId.Value}' is '{definition.GetType().Name}' and cannot be used as '{definitionType.Name}'.";
                throw new InvalidCastException(message);
            }
        }

        private TDefinition CastDefinition<TDefinition>(IEntityDefinition definition, Type definitionType) where TDefinition : class, IEntityDefinition
        {
            TDefinition typedDefinition = definition as TDefinition;
            if (typedDefinition == null)
            {
                string message = $"Definition '{definitionType.Name}' id '{definition.DefinitionId.Value}' cannot be cast to '{typeof(TDefinition).Name}'.";
                throw new InvalidCastException(message);
            }
            return typedDefinition;
        }
    }
}
