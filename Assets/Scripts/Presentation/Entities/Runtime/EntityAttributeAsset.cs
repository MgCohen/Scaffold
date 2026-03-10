using Scaffold.Entities;
using UnityEngine;

namespace Scaffold.Presentation.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entities/Attribute")]
    public sealed class EntityAttributeAsset : ScriptableObject
    {
        public EntityAttribute Value = new EntityAttribute();

        public static implicit operator EntityAttribute(EntityAttributeAsset asset)
        {
            if (asset == null) { return null; }
            return asset.Value;
        }

        public static implicit operator EntityAttributeAsset(EntityAttribute value)
        {
            EntityAttributeAsset asset = CreateInstance<EntityAttributeAsset>();
            asset.Value = value;
            return asset;
        }
    }
}
