using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public abstract class EntityDefinition<TInstance> : IEntityDefinition where TInstance : class, IEntityInstance
    {
        private readonly IReadOnlyDictionary<AttributeDefinitionId, int> baseAttributes;

        protected EntityDefinition(EntityDefinitionId definitionId, IReadOnlyDictionary<AttributeDefinitionId, int> baseAttributes)
        {
            DefinitionId = definitionId;
            DefinitionType = GetType();
            InstanceType = typeof(TInstance);
            this.baseAttributes = new Dictionary<AttributeDefinitionId, int>(baseAttributes);
        }

        public EntityDefinitionId DefinitionId { get; }
        public Type DefinitionType { get; }
        public Type InstanceType { get; }
        public IReadOnlyDictionary<AttributeDefinitionId, int> BaseAttributes
        {
            get
            {
                return baseAttributes;
            }
        }

        public IEntityInstance CreateInstance(EntityInstanceId instanceId)
        {
            AttributeBag attributeBag = new AttributeBag(BaseAttributes);
            TInstance instance = CreateTypedInstance(instanceId, attributeBag);
            return instance;
        }

        protected abstract TInstance CreateTypedInstance(EntityInstanceId instanceId, AttributeBag attributeBag);
    }
}
