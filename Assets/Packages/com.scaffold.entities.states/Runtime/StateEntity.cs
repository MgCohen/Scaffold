using Scaffold.Entities;

namespace Scaffold.Entities.States
{
    public sealed class StateEntity<TDefinition> : BaseEntityInstance<TDefinition>
        where TDefinition : IEntityDefinition
    {
        internal void Setup(InstanceId id, TDefinition definition, StoreVariableStorage storage)
        {
            Initialize(id, definition, storage);
        }
    }
}
