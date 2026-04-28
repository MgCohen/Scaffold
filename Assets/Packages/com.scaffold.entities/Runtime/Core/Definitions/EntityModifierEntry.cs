#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class EntityModifierEntry
    {
        public EntityModifierEntry(Variable key, VariableValue modifierValue)
        {
            this.key = key;
            this.modifierValue = modifierValue;
        }

        public EntityModifierEntry()
        {
        }

        public Variable Key
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

                return key ?? new Variable(string.Empty);
            }
        }

        public VariableValue ModifierValue => modifierValue;

        [SerializeField] private Variable? key;

        [SerializeField]
        [FormerlySerializedAs("variable")]
        private VariableSO variableLegacy;

        [SerializeReference]
        private VariableValue modifierValue;

        internal void RebaseSerializedModifierPayloadIfMismatch()
        {
            Variable k = Key;
            if (string.IsNullOrEmpty(k.Key))
            {
                return;
            }

            if (!VariablePayloadTypeHelpers.TryResolvePayload(k, nameof(EntityModifierEntry), out Type expected))
            {
                return;
            }

            if (modifierValue != null && modifierValue.GetType() == expected)
            {
                return;
            }

            modifierValue = VariableValueFactory.CreateDefault(expected);
        }
    }
}
