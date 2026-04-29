using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("bool")]
    public sealed class BoolVariableValue : VariableValue<bool>
    {
        public BoolVariableValue()
        {
        }

        public BoolVariableValue(bool initial)
        {
            value = initial;
        }

        public bool Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private bool value;

        public override bool Get()
        {
            return value;
        }

        protected override VariableValue<bool> WithValue(bool next)
        {
            return new BoolVariableValue(next);
        }
    }
}
