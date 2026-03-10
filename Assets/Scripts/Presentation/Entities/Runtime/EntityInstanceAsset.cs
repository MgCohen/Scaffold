using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entities/Instance")]
    public sealed class EntityInstanceAsset : ScriptableObject
    {
        [SerializeField] private EntityInstance<EntityDefinition> value = new EntityInstance<EntityDefinition>();

        public EntityInstance<EntityDefinition> Value
        {
            get { return value; }
            set { this.value = value; }
        }

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
