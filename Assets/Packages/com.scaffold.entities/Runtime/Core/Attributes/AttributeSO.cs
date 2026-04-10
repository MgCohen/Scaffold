using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Attribute", fileName = "Attribute")]
    public class AttributeSO : ScriptableObject
    {
        public AttributeValueType ValueType => valueType;

        public string CustomValueTypeName => customValueTypeName;

        [SerializeField]
        private AttributeValueType valueType = AttributeValueType.String;

        [SerializeField]
        private string customValueTypeName = string.Empty;

        internal void SetValueType(AttributeValueType valueType)
        {
            this.valueType = valueType;
        }

        internal void SetCustomValueTypeName(string assemblyQualifiedOrFullName)
        {
            customValueTypeName = assemblyQualifiedOrFullName ?? string.Empty;
        }

        internal bool TryResolveConcreteValueType(out Type concreteType)
        {
            if (valueType == AttributeValueType.Custom)
            {
                return TryParseConcreteType(customValueTypeName, out concreteType);
            }

            return AttributeValueRegistry.TryGetConcreteType(valueType, out concreteType!);
        }

        private static bool TryParseConcreteType(string name, out Type concreteType)
        {
            concreteType = null!;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string trimmed = name.Trim();
            concreteType = Type.GetType(trimmed, throwOnError: false, ignoreCase: false);
            return concreteType != null;
        }

        public static implicit operator Attribute(AttributeSO so)
        {
            if (so == null)
            {
                return new Attribute(string.Empty);
            }

            return new Attribute(so.name, so.ValueType, so.customValueTypeName);
        }
    }
}
