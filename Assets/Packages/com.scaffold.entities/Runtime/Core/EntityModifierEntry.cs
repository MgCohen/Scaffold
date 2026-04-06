using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Instance-only modifier contribution for a given <see cref="AttributeSO"/> slot.
    /// </summary>
    [Serializable]
    public sealed class EntityModifierEntry
    {
        public EntityModifierEntry(AttributeSO attribute, string contribution)
        {
            this.attribute = attribute;
            this.contribution = contribution ?? string.Empty;
        }

        public EntityModifierEntry()
        {
        }

        public AttributeSO Attribute => attribute;

        [SerializeField]
        private AttributeSO attribute = default!;

        public string Contribution => contribution ?? string.Empty;

        [SerializeField]
        private string contribution = string.Empty;
    }
}
