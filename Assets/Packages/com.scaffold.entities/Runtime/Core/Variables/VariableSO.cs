using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Variable", fileName = "Variable")]
    public class VariableSO : ScriptableObject
    {
        public VariableValueType ValueType => valueType;

        [SerializeField]
        private VariableValueType valueType = VariableValueType.String;

        internal void SetValueType(VariableValueType valueType)
        {
            this.valueType = valueType;
        }

        public static implicit operator Variable(VariableSO so)
        {
            if (so == null)
            {
                return new Variable(string.Empty);
            }

            return new Variable(so.name, so.ValueType);
        }
    }
}
