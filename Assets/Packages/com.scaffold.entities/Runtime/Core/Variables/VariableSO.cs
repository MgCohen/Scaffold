using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Variable", fileName = "Variable")]
    public class VariableSO : ScriptableObject
    {
        public VariableValueType ValueType => valueType;
        [SerializeField] private VariableValueType valueType = VariableValueType.String;

        public string Description => description;
        [SerializeField][TextArea(3, 10)] private string description = string.Empty;

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
