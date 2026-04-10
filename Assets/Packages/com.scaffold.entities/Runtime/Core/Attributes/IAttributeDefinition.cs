using System;

namespace Scaffold.Entities
{
    public interface IAttributeDefinition
    {
        Type ValueType { get; }

        AttributeValue CreateDefault();
    }
}
