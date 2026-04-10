using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class BoolScalarAttributeDefinition : GenericScalarAttributeDefinition<bool, BoolAttributeValue>
    {
        public override AttributeValueType? MapsToLegacyValueType => AttributeValueType.Bool;
    }
}
