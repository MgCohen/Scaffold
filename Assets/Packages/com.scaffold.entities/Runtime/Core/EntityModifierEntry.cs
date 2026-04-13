#nullable enable
using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityModifierEntry
    {
        public EntityModifierEntry(VariableSO variable, VariableValue modifierValue)
        {
            this.variable = variable;
            this.modifierValue = modifierValue;
        }

        public EntityModifierEntry(Variable key, VariableValue modifierValue)
        {
            variableKey = key;
            this.modifierValue = modifierValue;
        }

        public EntityModifierEntry()
        {
        }

        public VariableSO Variable => variable;

        public Variable Key => variableKey ?? (Variable)variable;

        public VariableValue ModifierValue => modifierValue;

        [SerializeField]
        private VariableSO variable = default!;

        [NonSerialized]
        private Variable? variableKey;

        [SerializeReference]
        private VariableValue modifierValue;
    }
}
