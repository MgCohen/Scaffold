using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("float")]
    public sealed class FloatVariableValue : VariableValue<float>
    {
        public FloatVariableValue()
        {
        }

        public FloatVariableValue(float initial)
        {
            value = initial;
        }

        public float Value
        {
            get => value;
            set => this.value = value;
        }

        [SerializeField]
        private float value;

        public override float Get()
        {
            return value;
        }

        protected override VariableValue<float> WithValue(float next)
        {
            return new FloatVariableValue(next);
        }
    }
}
