using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("float")]
    public sealed class FloatVariableValue : VariableValue, IVariableValue<float>
    {
        public float Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private float value;

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

            return new FloatVariableValue { Value = sum };
        }
    }
}
