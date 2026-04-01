using System;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Adds <see cref="Delta"/> to the effective value of <see cref="Attribute"/> on <see cref="Entity"/>.
    /// </summary>
    [Serializable]
    public sealed class EntityAttributeModifierEntry
    {
        public EntityAttributeModifierEntry(EntityAttribute attribute, float delta)
        {
            this.attribute = attribute;
            this.delta = delta;
        }

        public EntityAttribute Attribute => attribute;

        public float Delta => delta;

        [SerializeField]
        private EntityAttribute attribute;

        [SerializeField]
        private float delta;
    }
}
