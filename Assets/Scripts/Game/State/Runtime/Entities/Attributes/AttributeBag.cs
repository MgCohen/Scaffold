using System;
using System.Collections.Generic;

namespace Scaffold.Entities
{
    public class AttributeBag
    {
        private readonly Dictionary<AttributeDefinitionId, int> baseValues;
        private readonly Dictionary<AttributeDefinitionId, int> currentValues;
        private readonly Dictionary<TagDefinitionId, int> tagValues;
        private readonly Dictionary<ModifierInstanceId, AttributeModifier> modifiers;

        public AttributeBag()
        {
            baseValues = new Dictionary<AttributeDefinitionId, int>();
            currentValues = new Dictionary<AttributeDefinitionId, int>();
            tagValues = new Dictionary<TagDefinitionId, int>();
            modifiers = new Dictionary<ModifierInstanceId, AttributeModifier>();
        }

        public AttributeBag(IReadOnlyDictionary<AttributeDefinitionId, int> initialBaseValues) : this()
        {
            CopyBaseValues(initialBaseValues);
            Recalculate();
        }

        public IReadOnlyDictionary<AttributeDefinitionId, int> BaseValues
        {
            get
            {
                return baseValues;
            }
        }

        public IReadOnlyDictionary<AttributeDefinitionId, int> CurrentValues
        {
            get
            {
                return currentValues;
            }
        }

        public IReadOnlyDictionary<TagDefinitionId, int> TagValues
        {
            get
            {
                return tagValues;
            }
        }

        public IReadOnlyCollection<AttributeModifier> Modifiers
        {
            get
            {
                return modifiers.Values;
            }
        }

        public void SetBaseValue(AttributeDefinitionId attributeDefinitionId, int value)
        {
            baseValues[attributeDefinitionId] = value;
            Recalculate();
        }

        public bool RemoveBaseValue(AttributeDefinitionId attributeDefinitionId)
        {
            bool removed = baseValues.Remove(attributeDefinitionId);
            Recalculate();
            return removed;
        }

        public bool TryGetBaseValue(AttributeDefinitionId attributeDefinitionId, out int value)
        {
            bool found = baseValues.TryGetValue(attributeDefinitionId, out int baseValue);
            value = baseValue;
            return found;
        }

        public bool TryGetCurrentValue(AttributeDefinitionId attributeDefinitionId, out int value)
        {
            bool found = currentValues.TryGetValue(attributeDefinitionId, out int currentValue);
            value = currentValue;
            return found;
        }

        public void SetTagValue(TagDefinitionId tagDefinitionId, int value)
        {
            tagValues[tagDefinitionId] = value;
            Recalculate();
        }

        public bool RemoveTagValue(TagDefinitionId tagDefinitionId)
        {
            bool removed = tagValues.Remove(tagDefinitionId);
            Recalculate();
            return removed;
        }

        public bool TryGetTagValue(TagDefinitionId tagDefinitionId, out int value)
        {
            bool found = tagValues.TryGetValue(tagDefinitionId, out int tagValue);
            value = tagValue;
            return found;
        }

        public void AddModifier(AttributeModifier modifier)
        {
            modifiers[modifier.ModifierInstanceId] = modifier;
            Recalculate();
        }

        public bool RemoveModifier(ModifierInstanceId modifierInstanceId)
        {
            bool removed = modifiers.Remove(modifierInstanceId);
            Recalculate();
            return removed;
        }

        public void Recalculate()
        {
            currentValues.Clear();
            CopyCurrentFromBaseValues();
            List<AttributeModifier> orderedModifiers = GetOrderedModifiers();
            ApplyModifiers(orderedModifiers);
        }

        private void CopyBaseValues(IReadOnlyDictionary<AttributeDefinitionId, int> initialBaseValues)
        {
            foreach (KeyValuePair<AttributeDefinitionId, int> entry in initialBaseValues)
            {
                baseValues[entry.Key] = entry.Value;
            }
        }

        private void CopyCurrentFromBaseValues()
        {
            foreach (KeyValuePair<AttributeDefinitionId, int> entry in baseValues)
            {
                currentValues[entry.Key] = entry.Value;
            }
        }

        private List<AttributeModifier> GetOrderedModifiers()
        {
            List<AttributeModifier> orderedModifiers = new List<AttributeModifier>(modifiers.Values);
            orderedModifiers.Sort(CompareModifiers);
            return orderedModifiers;
        }

        private void ApplyModifiers(List<AttributeModifier> orderedModifiers)
        {
            foreach (AttributeModifier modifier in orderedModifiers)
            {
                ApplyModifier(modifier);
            }
        }

        private void ApplyModifier(AttributeModifier modifier)
        {
            int currentValue = GetCurrentValue(modifier.AttributeDefinitionId);
            int nextValue = ApplyOperation(currentValue, modifier);
            SetCurrentValue(modifier.AttributeDefinitionId, nextValue);
        }

        private int GetCurrentValue(AttributeDefinitionId attributeDefinitionId)
        {
            bool hasValue = currentValues.TryGetValue(attributeDefinitionId, out int currentValue);
            if (!hasValue)
            {
                currentValue = 0;
            }
            return currentValue;
        }

        private void SetCurrentValue(AttributeDefinitionId attributeDefinitionId, int value)
        {
            currentValues[attributeDefinitionId] = value;
        }

        private int ApplyOperation(int currentValue, AttributeModifier modifier)
        {
            int nextValue = currentValue;
            if (modifier.Operation == ModifierOperation.Add)
            {
                nextValue = currentValue + modifier.Value;
            }
            if (modifier.Operation == ModifierOperation.Override)
            {
                nextValue = modifier.Value;
            }
            if (modifier.Operation == ModifierOperation.Min)
            {
                nextValue = Math.Min(currentValue, modifier.Value);
            }
            if (modifier.Operation == ModifierOperation.Max)
            {
                nextValue = Math.Max(currentValue, modifier.Value);
            }
            return nextValue;
        }

        private static int CompareModifiers(AttributeModifier left, AttributeModifier right)
        {
            int comparison = left.Priority.CompareTo(right.Priority);
            if (comparison == 0)
            {
                comparison = left.Operation.CompareTo(right.Operation);
            }
            if (comparison == 0)
            {
                comparison = left.ModifierInstanceId.Value.CompareTo(right.ModifierInstanceId.Value);
            }
            return comparison;
        }
    }
}
