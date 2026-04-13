using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class StringVariableValue : VariableValue, IVariableValue<string>
    {
        public override VariableValueType Type => VariableValueType.String;

        public string Value
        {
            get => value;
            set => this.value = value ?? string.Empty;
        }

        [SerializeField]
        private string value = string.Empty;

        public string Get()
        {
            return Value;
        }

        public override VariableValue Combine(IReadOnlyList<VariableValue> contributions)
        {
            string result = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is StringVariableValue s)
                {
                    result += s.Value;
                }
            }

            return new StringVariableValue { Value = result };
        }
    }
}
