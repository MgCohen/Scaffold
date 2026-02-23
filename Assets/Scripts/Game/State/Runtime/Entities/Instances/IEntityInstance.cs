namespace Scaffold.Entities
{
    public interface IEntityInstance
    {
        EntityInstanceId InstanceId { get; }
        EntityDefinitionId DefinitionId { get; }
        AttributeBag Attributes { get; }
    }
}
