using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    [Serializable]
    public class EntityDefinition
    {
        private string id;
        private Dictionary<string, EntityAttribute> attributes = new Dictionary<string, EntityAttribute>();

        public string Id
        {
            get { return id; }
            set { id = value; }
        }

        public Dictionary<string, EntityAttribute> Attributes
        {
            get { return attributes; }
            set { attributes = value ?? new Dictionary<string, EntityAttribute>(); }
        }

        public bool TryGetBaseAttributeValue(string key, out double value)
        {
            value = default;
            bool hasAttribute = Attributes.TryGetValue(key, out EntityAttribute attribute);
            if (!hasAttribute) { return false; }
            if (attribute == null) { return false; }
            value = attribute.Value;
            return true;
        }
    }
}
