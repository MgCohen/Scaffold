using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class IntVariableValue : VariableValue, IVariableValue<int>
    {
        public override VariableValueType Type => VariableValueType.Int;

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
            set => this.min = value;
        }

        [SerializeField]
        private int min = int.MinValue;

        public int Max
        {
            get => max;
            set => this.max = value;
        }

        [SerializeField]
        private int max = int.MaxValue;

        public int Get()
        {
            return Value;
        }

        public override VariableValue Combine(IReadOnlyList<VariableValue> contributions)
        {
            int sum = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is IntVariableValue n)
                {
                    sum += n.Value;
                }
            }

            int clamped = Math.Clamp(sum, Min, Max);
            return new IntVariableValue { Value = clamped, Min = Min, Max = Max };
        }
    }
}
