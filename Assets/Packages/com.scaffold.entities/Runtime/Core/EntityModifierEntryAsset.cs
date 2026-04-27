#nullable enable
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(
        menuName = "Scaffold/Entity/Modifier Entry",
        fileName = "ModifierEntry",
        order = 1)]
    public sealed class EntityModifierEntryAsset : ScriptableObject
    {
        public EntityModifierEntry Entry => entry;

        [SerializeField]
        private EntityModifierEntry entry = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            entry.EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy();
            entry.RebaseSerializedModifierPayloadIfMismatch();
        }
#endif

        public static explicit operator EntityModifierEntry(EntityModifierEntryAsset? asset)
        {
            return asset == null ? null! : asset.entry;
        }
    }
}
