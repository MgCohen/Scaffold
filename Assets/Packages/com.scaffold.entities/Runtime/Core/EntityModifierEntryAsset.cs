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

        private EntityModifierEntry ToRuntimeEntry()
        {
            VariableSO? variableSo = entry.Variable;
            VariableValue? payload = entry.ModifierValue;
            if (variableSo == null)
            {
                return new EntityModifierEntry();
            }

            return new EntityModifierEntry(variableSo, payload);
        }

        public static explicit operator EntityModifierEntry(EntityModifierEntryAsset? asset)
        {
            return asset == null ? null! : asset.ToRuntimeEntry();
        }
    }
}
