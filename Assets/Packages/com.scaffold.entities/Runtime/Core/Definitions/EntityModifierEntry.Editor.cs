#if UNITY_EDITOR
using UnityEngine;
using Variable = Scaffold.Variables.Variable;

namespace Scaffold.Entities
{
    public sealed partial class EntityModifierEntry
    {
        [SerializeField]
        private VariableSO variableAuthoring;

        internal void EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy()
        {
            if (variableAuthoring == null)
            {
                return;
            }

            key = (Variable)variableAuthoring;
            payloadTypeId = variableAuthoring.PayloadTypeId;
            variableLegacy = null;
        }
    }
}
#endif
