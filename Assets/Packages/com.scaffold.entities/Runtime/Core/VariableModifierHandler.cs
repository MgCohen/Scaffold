using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class VariableModifierHandler
    {
        internal VariableModifierHandler()
        {
            modifiersByVariable = new Dictionary<Variable, List<EntityModifierEntry>>();
            scratch = new List<VariableValue>();
        }

        internal IEnumerable<Variable> ModifiedVariables => modifiersByVariable.Keys;

        private readonly Dictionary<Variable, List<EntityModifierEntry>> modifiersByVariable;
        private readonly List<VariableValue> scratch;

        internal void AddModifier(EntityModifierEntry entry)
        {
            if (entry == null || entry.ModifierValue == null)
            {
                return;
            }

            Variable key = entry.Key;
            if (!modifiersByVariable.TryGetValue(key, out List<EntityModifierEntry> bucket))
            {
                bucket = new List<EntityModifierEntry>();
                modifiersByVariable[key] = bucket;
            }

            bucket.Add(entry);
        }

        internal bool RemoveModifier(EntityModifierEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            Variable key = entry.Key;
            if (!modifiersByVariable.TryGetValue(key, out List<EntityModifierEntry> bucket))
            {
                return false;
            }

            bool removed = bucket.Remove(entry);
            if (removed && bucket.Count == 0)
            {
                modifiersByVariable.Remove(key);
            }

            return removed;
        }

        internal void ClearModifiers()
        {
            modifiersByVariable.Clear();
        }

        internal bool HasModifiersFor(Variable key)
        {
            return modifiersByVariable.ContainsKey(key);
        }

        internal void ClearModifiersForKey(Variable key)
        {
            modifiersByVariable.Remove(key);
        }

        internal VariableValue GetEffective(Variable key, VariableValue baseValue)
        {
            if (baseValue == null)
            {
                return null;
            }

            if (!modifiersByVariable.TryGetValue(key, out List<EntityModifierEntry> bucket) || bucket.Count == 0)
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
