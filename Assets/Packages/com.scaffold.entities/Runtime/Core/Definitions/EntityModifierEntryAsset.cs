#nullable enable
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(
        menuName = "Scaffold/Entity/Modifier Entry",
        fileName = "ModifierEntry",
        order = 1)]
    public sealed partial class EntityModifierEntryAsset : ScriptableObject
    {
        public EntityModifierEntry Entry => entry;

        [SerializeField]
        private EntityModifierEntry entry = new();

        public static explicit operator EntityModifierEntry(EntityModifierEntryAsset? asset)
        {
            return asset == null ? null! : asset.entry;
        }
    }
}
