#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class EntityModifierEntry
    {
        public EntityModifierEntry(Variable key, VariableModifier modifier)
        {
            this.key = key;
            this.modifier = modifier;
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

        public VariableModifier? Modifier => modifier;

        [SerializeField] private Variable? key;

        [SerializeField]
        [FormerlySerializedAs("variable")]
        private VariableSO variableLegacy;

        [SerializeReference]
        private VariableModifier? modifier;

        internal void RebaseSerializedModifierPayloadIfMismatch()
        {
            Variable k = Key;
            if (string.IsNullOrEmpty(k.Key))
            {
                return;
            }

            if (!TryLoadExpectedValueType(k, out Type? expectedValueType, out Type wrapperType))
            {
                return;
            }

            if (IsModifierCompatible(expectedValueType))
            {
                return;
            }

            ApplyFirstCandidateOrWarn(expectedValueType, k, wrapperType);
        }

        private bool TryLoadExpectedValueType(Variable k, out Type? expectedValueType, out Type wrapperType)
        {
            expectedValueType = null;
            wrapperType = null!;
            if (!VariablePayloadTypeHelpers.TryResolvePayload(k, nameof(EntityModifierEntry), out Type wt))
            {
                return false;
            }

            wrapperType = wt;
            expectedValueType = VariablePayloadTypeHelpers.ExtractValueType(wrapperType);
            if (expectedValueType != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(EntityModifierEntry)}: wrapper type '{wrapperType.Name}' has no IVariableValue<T> for key '{k.Key}'. Skipping modifier rebase.");
            return false;
        }

        private bool IsModifierCompatible(Type? expectedValueType)
        {
            return modifier != null
                && ModifierTypeIndex.TryGetValueType(modifier.GetType(), out Type modifierValueType)
                && modifierValueType == expectedValueType;
        }

        private void ApplyFirstCandidateOrWarn(Type expectedValueType, Variable k, Type wrapperType)
        {
            IReadOnlyList<Type> candidates = ModifierTypeIndex.ModifiersFor(expectedValueType);
            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(EntityModifierEntry)}: no VariableModifier types for value type '{expectedValueType.Name}' (key '{k.Key}', wrapper '{wrapperType.Name}').");
                return;
            }

            modifier = (VariableModifier)Activator.CreateInstance(candidates[0])!;
        }
    }
}
