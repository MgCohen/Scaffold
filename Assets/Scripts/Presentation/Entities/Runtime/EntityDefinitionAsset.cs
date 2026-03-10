using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entities/Definition")]
    public sealed class EntityDefinitionAsset : ScriptableObject
    {
        public EntityDefinition Value = new EntityDefinition();

        public static implicit operator EntityDefinition(EntityDefinitionAsset asset)
        {
            if (asset == null) { return null; }
            return asset.Value;
        }

        public static implicit operator EntityDefinitionAsset(EntityDefinition value)
        {
            EntityDefinitionAsset asset = CreateInstance<EntityDefinitionAsset>();
            asset.Value = value;
            return asset;
        }
    }
}
