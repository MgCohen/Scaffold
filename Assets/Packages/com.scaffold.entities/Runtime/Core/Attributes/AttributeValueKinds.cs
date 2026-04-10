using UnityEngine;

namespace Scaffold.Entities
{
    public static class AttributeValueKinds
    {
        public static void SetGlobalRegistry(AttributeValueKindRegistrySO registry)
        {
            AttributeValueKindResolver.SetGlobalRegistry(registry);
        }
    }
}
