using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    /// <summary>
    /// One named attribute on <see cref="Entity"/>; <see cref="Value"/> is base plus modifiers.
    /// Optional runtime callback when the effective value changes (not serialized).
    /// </summary>
    [Serializable]
    public class EntityAttributeEntry
    {
        public EntityAttribute Attribute => attribute;

        [SerializeField]
        private EntityAttribute attribute;

        /// <summary>
        /// Serialized base stat; <see cref="Value"/> includes <see cref="Entity"/> modifiers.
        /// </summary>
        public float BaseValue => baseValue;

        [SerializeField]
        [FormerlySerializedAs("value")]
        private float baseValue;

        /// <summary>
        /// Base value plus sum of modifiers for this attribute (updated when modifiers or base change).
        /// </summary>
        public float Value => effectiveValue;

        [NonSerialized]
        private float effectiveValue;

        [NonSerialized]
        public Action<float> OnValueChanged = delegate { };

        public bool SetBaseValue(float newBaseValue)
        {
            if (Mathf.Approximately(baseValue, newBaseValue))
            {
                return false;
            }

            baseValue = newBaseValue;
            return true;
        }

        internal bool TrySetEffectiveValue(float newEffectiveValue)
        {
            if (Mathf.Approximately(effectiveValue, newEffectiveValue))
            {
                return false;
            }

            effectiveValue = newEffectiveValue;
            OnValueChanged?.Invoke(effectiveValue);
            return true;
        }
    }
}
