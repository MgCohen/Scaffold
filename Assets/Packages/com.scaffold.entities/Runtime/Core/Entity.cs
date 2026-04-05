using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    /// <summary>
    /// Float-backed attributes as <see cref="EntityAttribute"/> keys with storage in <see cref="attributeEntries"/>.
    /// Effective values are base + sum of <see cref="attributeModifiers"/>; recomputed when modifiers or bases change.
    /// Subclasses add typed accessors and game-specific rules.
    /// </summary>
    public class Entity : MonoBehaviour
    {
        public IReadOnlyList<EntityAttributeModifierEntry> AttributeModifiers => attributeModifiers;

        [SerializeField]
        private List<EntityAttributeModifierEntry> attributeModifiers = new List<EntityAttributeModifierEntry>();

        [SerializeField]
        private List<EntityAttributeEntry> attributeEntries = new List<EntityAttributeEntry>();

        [NonSerialized]
        private bool modifiersDirty = true;

        public event Action<EntityAttribute, float> AttributeValueChanged;

        private void Awake()
        {
            modifiersDirty = true;
            RecalculateAttributesIfDirty();
        }

        private void OnEnable()
        {
            modifiersDirty = true;
            RecalculateAttributesIfDirty();
        }

        public void AddAttributeModifier(EntityAttribute attribute, float delta)
        {
            if (attribute == null)
            {
                return;
            }

            EntityAttributeModifierEntry modifierEntry = new EntityAttributeModifierEntry(attribute, delta);
            attributeModifiers.Add(modifierEntry);
            RecalculateAttributes(force: true);
        }

        public bool RemoveAttributeModifier(EntityAttribute attribute, float delta)
        {
            if (attribute == null)
            {
                return false;
            }

            return TryRemoveOneModifier(attribute, delta);
        }

        public void RemoveAttributeModifiersAt(int startIndex, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (startIndex < 0 || startIndex > attributeModifiers.Count - count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startIndex),
                    $"Invalid modifier range: startIndex={startIndex}, count={count}, listCount={attributeModifiers.Count}.");
            }

            attributeModifiers.RemoveRange(startIndex, count);
            RecalculateAttributes(force: true);
        }

        public void ClearAttributeModifiers()
        {
            if (attributeModifiers.Count == 0)
            {
                return;
            }

            attributeModifiers.Clear();
            RecalculateAttributes(force: true);
        }

        public bool GetBoolAttribute(EntityAttribute attribute)
        {
            return GetFloatAttribute(attribute) > 0f;
        }

        public float GetFloatAttribute(EntityAttribute attribute)
        {
            return ReadFloatAttribute(attribute);
        }

        public void SetBoolAttribute(EntityAttribute attribute, bool newValue)
        {
            SetFloatAttribute(attribute, newValue ? 1f : 0f);
        }

        public void SetFloatAttribute(EntityAttribute attribute, float newBaseValue)
        {
            ApplyFloatBaseValue(attribute, newBaseValue);
        }

        private bool TryRemoveOneModifier(EntityAttribute attribute, float delta)
        {
            for (int i = 0; i < attributeModifiers.Count; i++)
            {
                if (!ModifierEntryMatches(attributeModifiers[i], attribute, delta))
                {
                    continue;
                }

                attributeModifiers.RemoveAt(i);
                RecalculateAttributes(force: true);
                return true;
            }

            return false;
        }

        private bool ModifierEntryMatches(EntityAttributeModifierEntry m, EntityAttribute attribute, float delta)
        {
            return m != null && m.Attribute == attribute && Mathf.Approximately(m.Delta, delta);
        }

        private float ReadFloatAttribute(EntityAttribute attribute)
        {
            if (attribute == null)
            {
                return 0f;
            }

            RecalculateAttributesIfDirty();
            return FindFloatInEntries(attribute);
        }

        private float FindFloatInEntries(EntityAttribute attribute)
        {
            for (int i = 0; i < attributeEntries.Count; i++)
            {
                EntityAttributeEntry entry = attributeEntries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Attribute == attribute)
                {
                    return entry.Value;
                }
            }

            return 0f;
        }

        private void ApplyFloatBaseValue(EntityAttribute attribute, float newBaseValue)
        {
            if (attribute == null)
            {
                return;
            }

            if (!TryUpdateBaseValueForAttribute(attribute, newBaseValue))
            {
                return;
            }

            modifiersDirty = true;
            RecalculateAttributes(force: true);
        }

        private bool TryUpdateBaseValueForAttribute(EntityAttribute attribute, float newBaseValue)
        {
            for (int i = 0; i < attributeEntries.Count; i++)
            {
                EntityAttributeEntry entry = attributeEntries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Attribute != attribute)
                {
                    continue;
                }

                return entry.SetBaseValue(newBaseValue);
            }

            return false;
        }

        private void RecalculateAttributesIfDirty()
        {
            RecalculateAttributes(force: false);
        }

        private void RecalculateAttributes(bool force)
        {
            if (!force && !modifiersDirty)
            {
                return;
            }

            modifiersDirty = false;
            Dictionary<EntityAttribute, float> sums = BuildModifierSumsByAttribute();
            ApplyEffectiveValuesFromSums(sums);
        }

        private Dictionary<EntityAttribute, float> BuildModifierSumsByAttribute()
        {
            var sums = new Dictionary<EntityAttribute, float>();
            for (int m = 0; m < attributeModifiers.Count; m++)
            {
                AddModifierToSums(sums, attributeModifiers[m]);
            }

            return sums;
        }

        private void AddModifierToSums(Dictionary<EntityAttribute, float> sums, EntityAttributeModifierEntry mod)
        {
            if (mod == null || mod.Attribute == null)
            {
                return;
            }

            if (sums.TryGetValue(mod.Attribute, out float existing))
            {
                sums[mod.Attribute] = existing + mod.Delta;
            }
            else
            {
                sums[mod.Attribute] = mod.Delta;
            }
        }

        private void ApplyEffectiveValuesFromSums(Dictionary<EntityAttribute, float> sums)
        {
            for (int i = 0; i < attributeEntries.Count; i++)
            {
                ApplyOneEntryFromSums(sums, attributeEntries[i]);
            }
        }

        private void ApplyOneEntryFromSums(Dictionary<EntityAttribute, float> sums, EntityAttributeEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            EntityAttribute attr = entry.Attribute;
            if (attr == null)
            {
                return;
            }

            float modifierTotal = sums.TryGetValue(attr, out float s) ? s : 0f;
            float effective = entry.BaseValue + modifierTotal;
            if (entry.TrySetEffectiveValue(effective))
            {
                AttributeValueChanged?.Invoke(attr, effective);
            }
        }
    }
}
