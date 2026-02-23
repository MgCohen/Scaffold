namespace Scaffold.Entities
{
    public abstract class EntityInstance : IEntityInstance
    {
        protected EntityInstance(EntityInstanceId instanceId, EntityDefinitionId definitionId, AttributeBag attributes)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Attributes = attributes;
        }

        public EntityInstanceId InstanceId { get; }
        public EntityDefinitionId DefinitionId { get; }
        public AttributeBag Attributes { get; }
    }
}
