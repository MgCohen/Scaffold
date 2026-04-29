using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class StringAppendModifier : VariableModifier<string>
    {
        public StringAppendModifier()
        {
        }

        public StringAppendModifier(string suffix)
        {
            this.suffix = suffix ?? string.Empty;
        }

        [SerializeField]
        private string suffix = string.Empty;

        public override string Apply(string current)
        {
            return current + (suffix ?? string.Empty);
        }
    }
}
