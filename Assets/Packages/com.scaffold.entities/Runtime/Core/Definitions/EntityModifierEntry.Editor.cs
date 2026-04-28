#if UNITY_EDITOR
using UnityEngine;

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
            variableLegacy = null;
        }
    }
}
#endif
