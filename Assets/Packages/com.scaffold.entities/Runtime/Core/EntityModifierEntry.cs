#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityModifierEntry
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

                return key ?? new Variable(string.Empty, VariableValueType.String);
            }
        }

        public VariableValue ModifierValue
        {
            get
            {
                return modifierValue;
            }
        }

        [SerializeField] private Variable? key;

        [SerializeField]
        [FormerlySerializedAs("variable")]
        private VariableSO variableLegacy;

        [SerializeReference]
        private VariableValue modifierValue;

#if UNITY_EDITOR
        [SerializeField]
        private VariableSO variableAuthoring;

        internal void EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy()
        {
            if (variableAuthoring == null)
            {
                return;
            }

            key = (Variable)variableAuthoring;
            variableLegacy = null;
        }

#endif

        internal void RebaseSerializedModifierPayloadIfMismatch()
        {
            Variable k = Key;
            if (string.IsNullOrEmpty(k.Key))
            {
                return;
            }

            VariableValueType expected = k.Type;
            if (modifierValue != null && modifierValue.Type == expected)
            {
                return;
            }

            modifierValue = VariableValueFactory.CreateDefault(expected);
        }
    }
}
