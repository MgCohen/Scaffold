using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class BoolAttributeValue : AttributeValue, IAttributeValue<bool>
    {
        public override AttributeValueType Type => AttributeValueType.Bool;

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

        public override AttributeValue Combine(IReadOnlyList<AttributeValue> contributions)
        {
            bool current = Value;
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i] is BoolAttributeValue b)
                {
                    current = b.Value;
                }
            }

            return new BoolAttributeValue { Value = current };
        }
    }
}
