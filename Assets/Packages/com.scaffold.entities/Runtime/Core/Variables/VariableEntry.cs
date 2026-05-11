#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class VariableEntry
    {
        public VariableEntry()
        {
        }

        internal VariableEntry(Variable key, VariableValue baseVal, string? payloadTypeId = null)
        {
            this.key = key;
            this.payloadTypeId = payloadTypeId
                ?? (baseVal != null && VariableValueRegistry.TryGetId(baseVal.GetType(), out string id) ? id : "string");
            baseValue = baseVal;
        }

        internal Variable Key
        {
            get
            {
                if (key != null && !string.IsNullOrEmpty(key.Id))
                {
                    return key;
                }

                if (variableLegacy != null)
                {
                    return (Variable)variableLegacy;
                }

                return key ?? new Variable(string.Empty, string.Empty);
            }
        }

        public string PayloadTypeId => payloadTypeId ?? "string";

        internal VariableValue? BaseValue => baseValue;

        [SerializeField] private Variable? key;

        [SerializeField] private string payloadTypeId = "string";

        [SerializeField]
        [FormerlySerializedAs("variable")]
        private VariableSO? variableLegacy;

        [SerializeReference][SerializeField] private VariableValue? baseValue;

        internal void RebaseSerializedPayloadIfMismatch()
        {
            Variable k = Key;
            if (string.IsNullOrEmpty(k.Id))
            {
                return;
            }

            if (!VariablePayloadTypeHelpers.TryResolvePayload(PayloadTypeId, k.Id, nameof(VariableEntry), out Type expected))
            {
                return;
            }

            if (baseValue != null && baseValue.GetType() == expected)
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
