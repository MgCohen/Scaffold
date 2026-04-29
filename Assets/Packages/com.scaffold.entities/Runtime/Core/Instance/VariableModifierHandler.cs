#nullable enable
using System.Collections.Generic;

namespace Scaffold.Entities
{
    internal sealed class VariableModifierHandler
    {
        internal IEnumerable<Variable> ModifiedVariables => modifiersByVariable.Keys;

        private readonly Dictionary<Variable, List<ActiveModifier>> modifiersByVariable = new();

        internal ModifierId AddModifier(EntityModifierEntry entry)
        {
            if (entry?.Modifier == null)
            {
                return default;
            }

            ModifierId modifierId = ModifierId.New();
            Variable key = entry.Key;
            if (!modifiersByVariable.TryGetValue(key, out List<ActiveModifier>? bucket))
            {
                bucket = new List<ActiveModifier>();
                modifiersByVariable[key] = bucket;
            }

            ActiveModifier slot = new ActiveModifier(modifierId, entry.Modifier);
            int insertAt = ComputeInsertIndex(bucket, slot.Modifier.Order);
            bucket.Insert(insertAt, slot);
            return modifierId;
        }

        internal bool RemoveModifier(Variable key, ModifierId id)
        {
            if (key == null || id.Id == default)
            {
                return false;
            }

            if (!modifiersByVariable.TryGetValue(key, out List<ActiveModifier>? bucket))
            {
                return false;
            }

            return TryRemoveModifierSlot(bucket, key, id);
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
                return null!;
            }

            if (!modifiersByVariable.TryGetValue(key, out List<ActiveModifier>? list) || list.Count == 0)
            {
                return baseValue;
            }

            return baseValue.ApplyModifiers(list);
        }

        private bool TryRemoveModifierSlot(List<ActiveModifier> bucket, Variable key, ModifierId id)
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

        private int ComputeInsertIndex(List<ActiveModifier> bucket, int order)
        {
            int i = 0;
            while (i < bucket.Count && bucket[i].Modifier.Order <= order)
            {
                i++;
            }

            return i;
        }
    }
}
