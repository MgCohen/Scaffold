#if UNITY_EDITOR
namespace Scaffold.Entities
{
    public sealed partial class EntityModifierEntryAsset
    {
        private void OnValidate()
        {
            entry.EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy();
            entry.RebaseSerializedModifierPayloadIfMismatch();
        }
    }
}
#endif
