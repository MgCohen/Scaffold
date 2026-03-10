using System;

namespace Scaffold.Entities
{
    [Serializable]
    public abstract class EntityModifier
    {
        public string Id;
        public string TargetAttributeKey;
        public bool IsTemporary;

        public bool TargetsAttribute(string key)
        {
            bool isKeyValid = !string.IsNullOrEmpty(key);
            if (!isKeyValid) { return false; }
            return TargetAttributeKey == key;
        }

        public abstract double Apply(double currentValue);
    }
}
