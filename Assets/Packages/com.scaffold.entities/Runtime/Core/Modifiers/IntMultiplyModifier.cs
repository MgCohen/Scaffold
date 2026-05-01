using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class IntMultiplyModifier : VariableModifier<int>
    {
        public IntMultiplyModifier()
        {
        }

        public IntMultiplyModifier(int factor)
        {
            this.factor = factor;
        }

        [SerializeField]
        private int factor;

        public override int Apply(int current)
        {
            return unchecked(current * factor);
        }
    }
}
