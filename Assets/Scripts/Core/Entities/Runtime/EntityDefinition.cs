using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    [Serializable]
    public class EntityDefinition
    {
        public string Id;
        public string DisplayName;
        public List<EntityAttribute> Attributes = new List<EntityAttribute>();

        public bool TryGetBaseAttributeValue(string key, out double value)
        {
            value = default;
            EntityAttribute attribute = FindAttribute(key);
            if (attribute == null) { return false; }
            value = attribute.Value;
            return true;
        }

        private EntityAttribute FindAttribute(string key)
        {
            if (Attributes == null) { return null; }
            for (int index = 0; index < Attributes.Count; index++) { EntityAttribute attribute = Attributes[index]; if (attribute != null && attribute.Key == key) { return attribute; } }
            return null;
        }
    }
}
