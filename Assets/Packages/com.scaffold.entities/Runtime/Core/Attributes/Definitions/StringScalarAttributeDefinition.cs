using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class StringScalarAttributeDefinition : GenericScalarAttributeDefinition<string, StringAttributeValue>
    {
        public override AttributeValueType? MapsToLegacyValueType => AttributeValueType.String;
    }
}
