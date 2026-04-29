using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class BoolOverrideModifier : VariableModifier<bool>
    {
        public BoolOverrideModifier()
        {
        }

        public BoolOverrideModifier(bool value)
        {
            this.value = value;
        }

        [SerializeField]
        private bool value;

        public override bool Apply(bool current)
        {
            return value;
        }
    }
}
