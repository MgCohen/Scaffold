using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class AttributeModifierHandler
    {
        internal AttributeModifierHandler()
        {
            modifiersByAttribute = new Dictionary<Attribute, List<EntityModifierEntry>>();
            scratch = new List<AttributeValue>();
        }

        internal IEnumerable<Attribute> ModifiedAttributes => modifiersByAttribute.Keys;

        private readonly Dictionary<Attribute, List<EntityModifierEntry>> modifiersByAttribute;
        private readonly List<AttributeValue> scratch;

        internal void AddModifier(EntityModifierEntry entry)
        {
            if (entry == null || entry.ModifierValue == null)
            {
                return;
            }

            Attribute key = entry.AttributeKey;
            if (!modifiersByAttribute.TryGetValue(key, out List<EntityModifierEntry> bucket))
            {
                bucket = new List<EntityModifierEntry>();
                modifiersByAttribute[key] = bucket;
            }

            bucket.Add(entry);
        }

        internal bool RemoveModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            Attribute key = entry.AttributeKey;
            if (!modifiersByAttribute.TryGetValue(key, out List<EntityModifierEntry> bucket))
            {
                return false;
            }

            bool removed = bucket.Remove(entry);
            if (removed && bucket.Count == 0)
            {
                modifiersByAttribute.Remove(key);
            }

            return removed;
        }

        internal void ClearModifiers()
        {
            modifiersByAttribute.Clear();
        }

        internal bool HasModifiersFor(Attribute key)
        {
            return modifiersByAttribute.ContainsKey(key);
        }

        internal void ClearModifiersForKey(Attribute key)
        {
            modifiersByAttribute.Remove(key);
        }

        internal AttributeValue GetEffective(Attribute key, AttributeValue baseValue)
        {
            if (baseValue == null)
            {
                return null;
            }

            if (!modifiersByAttribute.TryGetValue(key, out List<EntityModifierEntry> bucket) || bucket.Count == 0)
            {
                return baseValue;
            }

            FillScratch(bucket);
            return scratch.Count == 0 ? baseValue : baseValue.Combine(scratch);
        }

        private void FillScratch(List<EntityModifierEntry> bucket)
        {
            scratch.Clear();
            for (int i = 0; i < bucket.Count; i++)
            {
                EntityModifierEntry mod = bucket[i];
                if (mod?.ModifierValue != null)
                {
                    scratch.Add(mod.ModifierValue);
                }
            }
        }
    }
}
