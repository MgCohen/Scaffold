using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entities/Modifier")]
    public sealed class EntityModifierAsset : ScriptableObject
    {
        [SerializeReference] public EntityModifier Value;

        public static implicit operator EntityModifier(EntityModifierAsset asset)
        {
            if (asset == null) { return null; }
            return asset.Value;
        }

        public static implicit operator EntityModifierAsset(EntityModifier value)
        {
            EntityModifierAsset asset = CreateInstance<EntityModifierAsset>();
            asset.Value = value;
            return asset;
        }
    }
}
