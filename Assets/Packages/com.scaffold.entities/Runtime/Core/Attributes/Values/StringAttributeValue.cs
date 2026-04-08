using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class StringAttributeValue : AttributeValue, IAttributeValue<string>
    {
        public override AttributeValueType Type => AttributeValueType.String;

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

        public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
        {
            string result = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is StringAttributeValue s)
                {
                    result += s.Value;
                }
            }

            return new StringAttributeValue { Value = result };
        }
    }
}
