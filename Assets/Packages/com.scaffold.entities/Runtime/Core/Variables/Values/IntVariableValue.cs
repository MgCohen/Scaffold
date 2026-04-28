using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("int")]
    public sealed class IntVariableValue : VariableValue, IVariableValue<int>
    {
        public int Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private int value;

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

            return new IntVariableValue { Value = sum };
        }
    }
}
