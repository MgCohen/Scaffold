using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityDefinition
    {
        EntityDefinitionId DefinitionId { get; }
        Type DefinitionType { get; }
        Type InstanceType { get; }
        IReadOnlyDictionary<AttributeDefinitionId, int> BaseAttributes { get; }

        IEntityInstance CreateInstance(EntityInstanceId instanceId);
    }
}
