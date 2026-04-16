using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class FloatVariableValue : VariableValue, IVariableValue<float>
    {
        public override VariableValueType Type => VariableValueType.Float;

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
            set => this.min = value;
        }

        [SerializeField]
        private float min = 0;

        public float Max
        {
            get => max;
            set => this.max = value;
        }

        [SerializeField]
        private float max = 100;

        public bool Clamped
        {
            get => clamped;
            set => clamped = value;
        }

        [SerializeField]
        private bool clamped;

        public float Get()
        {
            return Value;
        }

        public override VariableValue Combine(IReadOnlyList<VariableValue> contributions)
        {
            float sum = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is FloatVariableValue f)
                {
                    sum += f.Value;
                }
            }

            if (clamped)
            {
                sum = Math.Clamp(sum, Min, Max);
            }
            return new FloatVariableValue { Value = sum, Min = Min, Max = Max };
        }
    }
}
