using System;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Variable", fileName = "Variable")]
    public class VariableSO : ScriptableObject
    {
        public string PayloadTypeId => payloadTypeId;

        [SerializeField]
        private string payloadTypeId = "string";

        public string Description => description;

        [SerializeField][TextArea(3, 10)] private string description = string.Empty;

        internal void SetPayloadType(Type payloadType)
        {
            if (payloadType == null)
            {
                throw new ArgumentNullException(nameof(payloadType));
            }

            if (!VariableValueRegistry.TryGetId(payloadType, out string id))
            {
                throw new ArgumentException(
                    $"Type '{payloadType.FullName}' is not a registered VariableValue payload type.",
                    nameof(payloadType));
            }

            payloadTypeId = id;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!VariableValueRegistry.TryResolve(payloadTypeId, out _))
            {
                Debug.LogError($"VariableSO '{name}': unknown payloadTypeId '{payloadTypeId}'.", this);
            }
        }
#endif

        public static implicit operator Variable(VariableSO so)
        {
            if (so == null)
            {
                return new Variable(string.Empty);
            }

            return new Variable(so.name, so.PayloadTypeId);
        }
    }
}
