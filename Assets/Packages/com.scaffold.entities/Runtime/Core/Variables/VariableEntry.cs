using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class VariableEntry
    {
        public VariableEntry()
        {
        }

        internal VariableEntry(Variable key, VariableValue baseVal)
        {
            this.key = key;
            baseValue = baseVal;
        }

        internal Variable Key
        {
            get
            {
                if (key != null && !string.IsNullOrEmpty(key.Key))
                {
                    return key;
                }

                if (variableLegacy != null)
                {
                    return (Variable)variableLegacy;
                }

                return key ?? new Variable(string.Empty, VariableValueType.String);
            }
        }

        internal VariableValue BaseValue
        {
            get
            {
                return baseValue;
            }
        }

        [SerializeField] private Variable? key;

        [SerializeField]
        [FormerlySerializedAs("variable")]
        private VariableSO variableLegacy;

        [SerializeReference][SerializeField] private VariableValue baseValue;

        internal void RebaseSerializedPayloadIfMismatch()
        {
            Variable k = Key;
            if (string.IsNullOrEmpty(k.Key))
            {
                return;
            }

            VariableValueType expected = k.Type;
            if (baseValue != null && baseValue.Type == expected)
            {
                return;
            }

            baseValue = VariableValueFactory.CreateDefault(expected);
        }

        internal static VariableEntry Create(Variable key, VariableValue baseVal)
        {
            return new VariableEntry(key, baseVal);
        }
    }
}
