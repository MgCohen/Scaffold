using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("int")]
    public sealed class IntVariableValue : VariableValue<int>
    {
        public IntVariableValue()
        {
        }

        public IntVariableValue(int initial)
        {
            value = initial;
        }

        public int Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private int value;

        public override int Get()
        {
            return value;
        }

        protected override VariableValue<int> WithValue(int next)
        {
            return new IntVariableValue(next);
        }
    }
}
