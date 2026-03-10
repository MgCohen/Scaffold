using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class EntityInstance<TDefinition> : IEntityInstance where TDefinition : EntityDefinition
    {
        public string Id { get; set; }
        public TDefinition Definition;
        private readonly Dictionary<string, AttributeModifierBucket> modifiers = new Dictionary<string, AttributeModifierBucket>();

        public string DefinitionId { get { return Definition.Id; } }

        public bool TryGetAttributeValue(string key, out double value)
        {
            value = default;
            bool hasModifier = TryGetModifiers(key, out AttributeModifierBucket bucket);
            if (!hasModifier) { return Definition.TryGetBaseAttributeValue(key, out value); }
            bool hasBase = Definition.TryGetBaseAttributeValue(key, out double baseValue);
            if (!hasBase) { return false; }
            value = bucket.CalculateValue(baseValue);
            return true;
        }

        public void AddModifier(string key, EntityModifier modifier)
        {
            if (string.IsNullOrEmpty(key) || modifier == null) { return; }
            AttributeModifierBucket bucket = EnsureBucket(key);
            bucket.AddModifier(modifier);
        }

        public bool RemoveModifier(string key, EntityModifier modifier)
        {
            bool hasBucket = modifiers.TryGetValue(key, out AttributeModifierBucket bucket);
            if (!hasBucket || modifier == null) { return false; }
            bool wasRemoved = bucket.RemoveModifier(modifier);
            bool shouldRemoveBucket = wasRemoved && !bucket.HasModifiers;
            if (shouldRemoveBucket) { modifiers.Remove(key); }
            return wasRemoved;
        }

        private bool TryGetModifiers(string key, out AttributeModifierBucket bucket)
        {
            bool hasBucket = modifiers.TryGetValue(key, out bucket);
            if (!hasBucket) { return false; }
            return bucket.HasModifiers;
        }

        private AttributeModifierBucket EnsureBucket(string key)
        {
            bool hasExisting = modifiers.TryGetValue(key, out AttributeModifierBucket existing);
            if (hasExisting) { return existing; }
            AttributeModifierBucket created = new AttributeModifierBucket();
            modifiers[key] = created;
            return created;
        }

        [Serializable]
        private sealed class AttributeModifierBucket
        {
            private readonly List<EntityModifier> orderedModifiers = new List<EntityModifier>();
            private bool hasModifiers;

            public bool HasModifiers { get { return hasModifiers; } }

            public void AddModifier(EntityModifier modifier)
            {
                orderedModifiers.Add(modifier);
                RefreshState();
            }

            public bool RemoveModifier(EntityModifier modifier)
            {
                bool removed = orderedModifiers.Remove(modifier);
                RefreshState();
                return removed;
            }

            public double CalculateValue(double baseValue)
            {
                double result = baseValue;
                for (int index = 0; index < orderedModifiers.Count; index++)
                {
                    EntityModifier modifier = orderedModifiers[index];
                    if (modifier != null) { result = modifier.Apply(result); }
                }
                return result;
            }

            private void RefreshState()
            {
                hasModifiers = orderedModifiers.Count > 0;
            }
        }
    }
}
