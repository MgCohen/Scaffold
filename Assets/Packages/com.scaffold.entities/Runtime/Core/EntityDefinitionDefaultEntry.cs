using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// One default attribute on an <see cref="EntityDefinition"/>; optional payload overrides <see cref="AttributeSO.DefaultPayload"/>.
    /// </summary>
    [Serializable]
    public sealed class EntityDefinitionDefaultEntry
    {
        public AttributeSO Attribute => attribute;

        [SerializeField]
        private AttributeSO attribute = default!;

        [SerializeField]
        private string payloadOverride = string.Empty;

        public Attribute GetDefaultAttribute()
        {
            if (attribute == null)
            {
                return default;
            }

            if (string.IsNullOrEmpty(payloadOverride))
            {
                return (Attribute)attribute;
            }

            return new Attribute(payloadOverride, attribute.name);
        }
    }
}
