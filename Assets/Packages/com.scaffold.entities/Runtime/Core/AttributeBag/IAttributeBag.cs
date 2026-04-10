using System.Collections.Generic;

namespace Scaffold.Entities
{
    public interface IAttributeBag
    {
        IAttributeBag Parent { get; }

        bool TryGetBase(Attribute key, out AttributeValue value);

        IEnumerable<Attribute> LocalKeys { get; }
    }
}
