using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class AttributeValue
    {
        public abstract AttributeValueType Type { get; }

        public abstract AttributeValue Combine(IReadOnlyList<AttributeValue> contributions);

        internal AttributeValue CloneShallow() => (AttributeValue)MemberwiseClone();
    }
}
