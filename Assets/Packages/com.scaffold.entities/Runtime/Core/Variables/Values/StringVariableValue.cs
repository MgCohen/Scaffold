using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    [VariableValueId("string")]
    public sealed class StringVariableValue : VariableValue<string>
    {
        public StringVariableValue()
        {
        }

        public StringVariableValue(string initial)
        {
            value = initial ?? string.Empty;
        }

        public string Value
        {
            get => value;
            set => this.value = value ?? string.Empty;
        }

        [SerializeField]
        private string value = string.Empty;

        public override string Get()
        {
            return value;
        }

        protected override VariableValue<string> WithValue(string next)
        {
            return new StringVariableValue(next);
        }
    }
}
