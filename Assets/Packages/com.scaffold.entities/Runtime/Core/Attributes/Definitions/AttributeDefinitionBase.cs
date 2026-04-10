using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class AttributeDefinitionBase
    {
        public string StableTypeId => string.IsNullOrEmpty(stableTypeId) ? GetType().Name : stableTypeId;

        public abstract Type ConcreteValueType { get; }

        public virtual AttributeValueType? MapsToLegacyValueType => null;

        public abstract AttributeValue CreateDefault();

        [SerializeField]
        private string stableTypeId = string.Empty;

        internal void SetStableTypeId(string id)
        {
            stableTypeId = id ?? string.Empty;
        }
    }
}
