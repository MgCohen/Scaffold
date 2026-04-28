using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("bool")]
    public sealed class BoolVariableValue : VariableValue, IVariableValue<bool>
    {
        public bool Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private bool value;

        public bool Get()
        {
            return Value;
        }

        public override VariableValue Combine(IReadOnlyList<VariableValue> contributions)
        {
            bool current = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is BoolVariableValue b)
                {
                    current = b.Value;
                }
            }

            return new BoolVariableValue { Value = current };
        }
    }
}
