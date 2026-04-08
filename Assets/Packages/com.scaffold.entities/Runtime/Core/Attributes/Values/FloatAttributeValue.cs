using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class FloatAttributeValue : AttributeValue, IAttributeValue<float>
    {
        public override AttributeValueType Type => AttributeValueType.Float;

        public float Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private float value;

        public float Min
        {
            get => min;
            set => min = value;
        }

        [SerializeField]
        private float min = float.MinValue;

        public float Max
        {
            get => max;
            set => max = value;
        }

        [SerializeField]
        private float max = float.MaxValue;

        public float Get()
        {
            return Value;
        }

        public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
        {
            float sum = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is FloatAttributeValue f)
                {
                    sum += f.Value;
                }
            }

            float clamped = Math.Clamp(sum, Min, Max);
            return new FloatAttributeValue { Value = clamped, Min = Min, Max = Max };
        }
    }
}
