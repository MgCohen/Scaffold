using System;

namespace Scaffold.Entities
{
    public interface IAttributeDefinition<T> : IAttributeDefinition
        where T : AttributeValue
    {
        new T CreateDefault();

        bool TryGetPayload(AttributeValue value, out T payload);
    }
}
