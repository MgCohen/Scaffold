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

            if (AttributeValueKindResolver.TryResolveDefinition(attribute, out AttributeDefinitionBase definition))
            {
                if (baseValue != null && baseValue.GetType() == definition.ConcreteValueType)
                {
                    return;
                }

                baseValue = definition.CreateDefault();
                return;
            }

            if (!attribute.TryResolveConcreteValueType(out Type requiredConcrete))
            {
                return;
            }

            if (baseValue != null && baseValue.GetType() == requiredConcrete)
            {
                return;
            }

            if (AttributeValueRegistry.TryCreate(requiredConcrete, out AttributeValue created))
            {
                baseValue = created;
            }
        }

        internal static AttributeEntry Create(AttributeSO attr, AttributeValue baseVal)
        {
            return new AttributeEntry(attr, baseVal);
        }
    }
}
