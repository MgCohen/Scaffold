using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class IntAddModifier : VariableModifier<int>
    {
        public IntAddModifier()
        {
        }

        public IntAddModifier(int amount)
        {
            this.amount = amount;
        }

        [SerializeField]
        private int amount;

        public override int Apply(int current)
        {
            return unchecked(current + amount);
        }
    }
}
