namespace Scaffold.Entities
{
    public interface IEntityInstance
    {
        string Id { get; }
        string DefinitionId { get; }

        bool TryGetAttributeValue(string key, out double value);
    }
}
