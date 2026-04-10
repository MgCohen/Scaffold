using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class FloatScalarAttributeDefinition : GenericScalarAttributeDefinition<float, FloatAttributeValue>
    {
        public override AttributeValueType? MapsToLegacyValueType => AttributeValueType.Float;
    }
}
