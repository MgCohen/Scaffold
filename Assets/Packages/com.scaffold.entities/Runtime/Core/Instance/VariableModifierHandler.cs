using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class VariableModifierHandler
    {
        internal VariableModifierHandler()
        {
            modifiersByVariable = new Dictionary<Variable, List<(ModifierId Id, EntityModifierEntry Entry)>>();
            scratch = new List<VariableValue>();
        }

        internal IEnumerable<Variable> ModifiedVariables => modifiersByVariable.Keys;

        private readonly Dictionary<Variable, List<(ModifierId Id, EntityModifierEntry Entry)>> modifiersByVariable;
        private readonly List<VariableValue> scratch;

        internal ModifierId AddModifier(EntityModifierEntry entry)
        {
            if (entry == null || entry.ModifierValue == null)
            {
                return default;
            }

            ModifierId modifierId = ModifierId.New();
            Variable key = entry.Key;
            if (!modifiersByVariable.TryGetValue(key, out List<(ModifierId Id, EntityModifierEntry Entry)> bucket))
            {
                bucket = new List<(ModifierId Id, EntityModifierEntry Entry)>();
                modifiersByVariable[key] = bucket;
            }

            bucket.Add((modifierId, entry));
            return modifierId;
        }

        internal bool RemoveModifier(Variable key, ModifierId id)
        {
            if (key == null || id.Id == default)
            {
                return false;
            }

            if (!modifiersByVariable.TryGetValue(key, out List<(ModifierId Id, EntityModifierEntry Entry)> bucket))
            {
                return false;
            }

            return TryRemoveModifierSlot(bucket, key, id);
        }

        private bool TryRemoveModifierSlot(List<(ModifierId Id, EntityModifierEntry Entry)> bucket, Variable key, ModifierId id)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id.Equals(id))
                {
                    bucket.RemoveAt(i);
                    if (bucket.Count == 0)
                    {
                        modifiersByVariable.Remove(key);
                    }

                    return true;
                }
            }

            return false;
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

            if (!modifiersByVariable.TryGetValue(key, out List<(ModifierId Id, EntityModifierEntry Entry)> bucket) ||
                bucket.Count == 0)
            {
                return baseValue;
            }

            FillScratch(bucket);
            return EntityVariableComputer.ComputeEffective(baseValue, scratch);
        }

        private void FillScratch(List<(ModifierId Id, EntityModifierEntry Entry)> bucket)
        {
            scratch.Clear();
            for (int i = 0; i < bucket.Count; i++)
            {
                EntityModifierEntry mod = bucket[i].Entry;
                if (mod?.ModifierValue != null)
                {
                    scratch.Add(mod.ModifierValue);
                }
            }
        }
    }
}
