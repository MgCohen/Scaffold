namespace Scaffold.Entities
{
    public interface IEntityRegistry
    {
        bool RegisterDefinition(EntityDefinition definition);
        bool RegisterInstance(IEntityInstance instance);
        bool TryGetDefinition(string id, out EntityDefinition definition);
        bool TryGetInstance(string id, out IEntityInstance instance);
        bool UnregisterDefinition(string id);
        bool UnregisterInstance(string id);
        void Clear();
    }
}
