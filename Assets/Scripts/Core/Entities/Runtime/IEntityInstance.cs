using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IEntityInstance
    {
        string Id { get; }
        EntityDefinition Definition { get; }
        IReadOnlyList<EntityModifier> Modifiers { get; }

        bool TryGetAttributeValue(string key, out double value);
    }
}
