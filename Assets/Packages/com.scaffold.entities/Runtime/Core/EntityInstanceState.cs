using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Serializable instance state: unique id, bound <see cref="EntityDefinition"/>, and instance-only modifiers.
    /// Use <see cref="EntityInstanceFactory"/> to create a fully initialized state.
    /// </summary>
    [Serializable]
    public sealed class EntityInstanceState
    {
        public InstanceId Id => id;

        [SerializeField]
        private InstanceId id;

        public EntityDefinition Definition => definition;

        [SerializeField]
        private EntityDefinition definition = default!;

        public IReadOnlyList<EntityModifierEntry> Modifiers => modifiers;

        [SerializeField]
        private List<EntityModifierEntry> modifiers = new List<EntityModifierEntry>();

        public void Initialize(InstanceId instanceId, EntityDefinition entityDefinition, List<EntityModifierEntry> modifierList)
        {
            id = instanceId;
            definition = entityDefinition;
            modifiers = modifierList ?? new List<EntityModifierEntry>();
            EnsureDefinitionLookup();
        }

        public void EnsureDefinitionLookup()
        {
            definition?.RebuildLookup();
        }

        public bool TryGetAttribute(Attribute template, out Attribute value)
        {
            value = default;
            if (template.MatchKey != null && definition != null &&
                definition.TryGetAttributeSOByName(template.MatchKey, out AttributeSO so))
            {
                return TryGetAttribute(so, out value);
            }

            return false;
        }

        public bool TryGetAttribute(AttributeSO attribute, out Attribute value)
        {
            value = default;
            if (attribute == null)
            {
                return false;
            }

            value = GetEffectiveAttribute(attribute);
            return true;
        }

        public bool TryGetAttribute(string match, out Attribute value)
        {
            return TryFindAttributeByStringScan(match, out value);
        }

        public void AddModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            modifiers.Add(entry);
        }

        public void ClearModifiers()
        {
            modifiers.Clear();
        }

        public bool RemoveModifierAt(int index)
        {
            if (index < 0 || index >= modifiers.Count)
            {
                return false;
            }

            modifiers.RemoveAt(index);
            return true;
        }

        private bool TryFindAttributeByStringScan(string match, out Attribute value)
        {
            value = default;
            if (string.IsNullOrEmpty(match))
            {
                return false;
            }

            foreach (AttributeSO slot in EnumerateAttributeSlots())
            {
                if (TryMatchSlotByString(match, slot, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryMatchSlotByString(string match, AttributeSO slot, out Attribute value)
        {
            value = default;
            if (slot == null)
            {
                return false;
            }

            Attribute effective = GetEffectiveAttribute(slot);
            if (!MatchesStringQuery(match, slot, effective))
            {
                return false;
            }

            value = effective;
            return true;
        }

        private Attribute GetEffectiveAttribute(AttributeSO attribute)
        {
            Attribute baseAttribute = definition != null ? definition.GetBaseAttribute(attribute) : (Attribute)attribute;
            var contributions = new List<string>();
            CollectContributions(attribute, contributions);
            string combined = AttributeCombine.Combine(baseAttribute.Payload, contributions);
            return new Attribute(combined, baseAttribute.MatchKey ?? attribute.name);
        }

        private void CollectContributions(AttributeSO attribute, List<string> contributions)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                EntityModifierEntry mod = modifiers[i];
                if (mod?.Attribute != attribute)
                {
                    continue;
                }

                contributions.Add(mod.Contribution);
            }
        }

        private IEnumerable<AttributeSO> EnumerateAttributeSlots()
        {
            var seen = new HashSet<AttributeSO>();
            foreach (AttributeSO slot in EnumerateDefinitionSlots(seen))
            {
                yield return slot;
            }

            foreach (AttributeSO slot in EnumerateModifierSlots(seen))
            {
                yield return slot;
            }
        }

        private IEnumerable<AttributeSO> EnumerateDefinitionSlots(HashSet<AttributeSO> seen)
        {
            if (definition == null)
            {
                yield break;
            }

            IReadOnlyList<EntityDefinitionDefaultEntry> defaults = definition.DefaultAttributes;
            for (int i = 0; i < defaults.Count; i++)
            {
                EntityDefinitionDefaultEntry entry = defaults[i];
                if (entry?.Attribute != null && seen.Add(entry.Attribute))
                {
                    yield return entry.Attribute;
                }
            }
        }

        private IEnumerable<AttributeSO> EnumerateModifierSlots(HashSet<AttributeSO> seen)
        {
            for (int m = 0; m < modifiers.Count; m++)
            {
                EntityModifierEntry mod = modifiers[m];
                if (mod?.Attribute != null && seen.Add(mod.Attribute))
                {
                    yield return mod.Attribute;
                }
            }
        }

        private bool MatchesStringQuery(string match, AttributeSO slot, Attribute effective)
        {
            if (effective.MatchKey != null && string.Equals(effective.MatchKey, match, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(slot.name, match, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(effective.Payload, match, StringComparison.Ordinal);
        }
    }
}
