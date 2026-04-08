using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Attribute", fileName = "Attribute")]
    public class AttributeSO : ScriptableObject
    {
        public AttributeValueType ValueType => valueType;

        [SerializeField]
        private AttributeValueType valueType = AttributeValueType.String;

        internal void SetValueType(AttributeValueType valueType)
        {
            this.valueType = valueType;
        }

        public static implicit operator Attribute(AttributeSO so)
        {
            if (so == null)
            {
                return new Attribute(string.Empty);
            }

            return new Attribute(so.name, so.ValueType);
        }
    }
}
