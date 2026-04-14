using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class VariableEntry
    {
        public VariableEntry()
        {
        }

        internal VariableEntry(VariableSO variable, VariableValue baseVal)
        {
            this.variable = variable;
            baseValue = baseVal;
        }

        internal VariableSO Variable => variable;
        [FormerlySerializedAs("attribute")]
        [SerializeField] private VariableSO variable;

        internal VariableValue BaseValue => baseValue;
        [SerializeReference][SerializeField] private VariableValue baseValue;

        internal void EnsureValueMatchesType()
        {
            if (variable == null)
            {
                return;
            }

            VariableValueType required = variable.ValueType;
            if (baseValue != null && baseValue.Type == required)
            {
                return;
            }

            baseValue = CreateDefaultForType(required);
        }

        internal static VariableEntry Create(VariableSO variable, VariableValue baseVal)
        {
            return new VariableEntry(variable, baseVal);
        }

        private static VariableValue CreateDefaultForType(VariableValueType required)
        {
            return required switch
            {
                VariableValueType.Float => new FloatVariableValue(),
                VariableValueType.Int => new IntVariableValue(),
                VariableValueType.Bool => new BoolVariableValue(),
                VariableValueType.String => new StringVariableValue(),
                _ => null!
            };
        }
    }
}
