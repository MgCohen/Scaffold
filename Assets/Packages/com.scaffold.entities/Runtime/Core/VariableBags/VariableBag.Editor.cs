#if UNITY_EDITOR
namespace Scaffold.Entities
{
    public sealed partial class VariableBag
    {
        internal void EditorApplyVariableAuthoringFromValidation()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                VariableEntry entry = entries[i];
                entry?.EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy();
                entry?.RebaseSerializedPayloadIfMismatch();
            }
        }
    }
}
#endif
