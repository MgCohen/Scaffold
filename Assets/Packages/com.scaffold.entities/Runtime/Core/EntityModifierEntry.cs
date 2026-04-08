#nullable enable
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityModifierEntry
    {
        public EntityModifierEntry(AttributeSO attribute, AttributeValue modifierValue)
        {
            this.attribute = attribute;
            this.modifierValue = modifierValue;
        }

        public EntityModifierEntry(Attribute key, AttributeValue modifierValue)
        {
            attributeKey = key;
            this.modifierValue = modifierValue;
        }

        public EntityModifierEntry()
        {
        }

        public AttributeSO Attribute => attribute;

        public Attribute AttributeKey => attributeKey ?? (Attribute)attribute;

        public AttributeValue ModifierValue => modifierValue;

        [SerializeField]
        private AttributeSO attribute = default!;

        [NonSerialized]
        private Attribute? attributeKey;

        [SerializeReference]
        private AttributeValue modifierValue;
    }
}
