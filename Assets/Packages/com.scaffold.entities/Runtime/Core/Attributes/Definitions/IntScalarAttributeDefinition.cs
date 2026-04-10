using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class IntScalarAttributeDefinition : GenericScalarAttributeDefinition<int, IntAttributeValue>
    {
        public override AttributeValueType? MapsToLegacyValueType => AttributeValueType.Int;
    }
}
