using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class FloatAddModifier : VariableModifier<float>
    {
        public FloatAddModifier()
        {
        }

        public FloatAddModifier(float amount)
        {
            this.amount = amount;
        }

        [SerializeField]
        private float amount;

        public override float Apply(float current)
        {
            return current + amount;
        }
    }
}
