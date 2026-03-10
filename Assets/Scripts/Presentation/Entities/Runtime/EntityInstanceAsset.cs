using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entities/Instance")]
    public sealed class EntityInstanceAsset : ScriptableObject
    {
        public EntityInstance<EntityDefinition> Value = new EntityInstance<EntityDefinition>();

        public static implicit operator EntityInstance<EntityDefinition>(EntityInstanceAsset asset)
        {
            if (asset == null) { return null; }
            return asset.Value;
        }

        public static implicit operator EntityInstanceAsset(EntityInstance<EntityDefinition> value)
        {
            EntityInstanceAsset asset = CreateInstance<EntityInstanceAsset>();
            asset.Value = value;
            return asset;
        }
    }
}
