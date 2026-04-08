using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class AttributeEntry
    {
        public AttributeEntry()
        {
        }

        internal AttributeEntry(AttributeSO attr, AttributeValue baseVal)
        {
            attribute = attr;
            baseValue = baseVal;
        }

        public AttributeSO Attribute => attribute;
        [SerializeField] private AttributeSO attribute;

        public AttributeValue BaseValue => baseValue;
        [SerializeReference][SerializeField] private AttributeValue baseValue;

        internal void EnsureValueMatchesType()
        {
            if (attribute == null)
            {
                return;
            }

            AttributeValueType required = attribute.ValueType;
            if (baseValue != null && baseValue.Type == required)
            {
                return;
            }

            baseValue = CreateDefaultForType(required);
        }

        internal static AttributeEntry Create(AttributeSO attr, AttributeValue baseVal)
        {
            return new AttributeEntry(attr, baseVal);
        }

        private static AttributeValue CreateDefaultForType(AttributeValueType required)
        {
            return required switch
            {
                AttributeValueType.Float => new FloatAttributeValue(),
                AttributeValueType.Int => new IntAttributeValue(),
                AttributeValueType.Bool => new BoolAttributeValue(),
                AttributeValueType.String => new StringAttributeValue(),
                _ => null
            };
        }
    }
}
