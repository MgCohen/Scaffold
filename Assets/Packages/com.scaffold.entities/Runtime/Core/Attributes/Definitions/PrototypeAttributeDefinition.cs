using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class PrototypeAttributeDefinition : AttributeDefinitionBase
    {
        [SerializeReference]
        [SerializeField]
        private AttributeValue prototype;

        public override Type ConcreteValueType =>
            prototype != null ? prototype.GetType() : typeof(AttributeValue);

        public override AttributeValueType? MapsToLegacyValueType =>
            prototype != null ? prototype.Type : null;

        public override AttributeValue CreateDefault()
        {
            if (prototype == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(PrototypeAttributeDefinition)} requires a non-null {nameof(prototype)}.");
            }

            return prototype.CloneShallow();
        }

        internal void SetPrototype(AttributeValue value)
        {
            prototype = value;
        }
    }
}
