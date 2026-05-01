using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class FloatMultiplyModifier : VariableModifier<float>
    {
        public FloatMultiplyModifier()
        {
        }

        public FloatMultiplyModifier(float factor)
        {
            this.factor = factor;
        }

        [SerializeField]
        private float factor;

        public override float Apply(float current)
        {
            return current * factor;
        }
    }
}
