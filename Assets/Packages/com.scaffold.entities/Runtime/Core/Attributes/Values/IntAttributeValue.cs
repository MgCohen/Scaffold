using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class IntAttributeValue : AttributeValue, IAttributeValue<int>
    {
        public override AttributeValueType Type => AttributeValueType.Int;

        public int Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private int value;

        public int Min
        {
            get => min;
            set => min = value;
        }

        [SerializeField]
        private int min = int.MinValue;

        public int Max
        {
            get => max;
            set => max = value;
        }

        [SerializeField]
        private int max = int.MaxValue;

        public int Get()
        {
            return Value;
        }

        public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
        {
            int sum = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is IntAttributeValue n)
                {
                    sum += n.Value;
                }
            }

            int clamped = Math.Clamp(sum, Min, Max);
            return new IntAttributeValue { Value = clamped, Min = Min, Max = Max };
        }
    }
}
