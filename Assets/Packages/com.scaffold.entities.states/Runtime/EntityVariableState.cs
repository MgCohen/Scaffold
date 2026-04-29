#nullable enable

using System;
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record EntityVariableState(IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State
    {
        public static EntityVariableState Empty { get; } = new EntityVariableState(new Dictionary<Variable, VariableValue>(), new Dictionary<Variable, IReadOnlyList<ActiveModifier>>());

        internal static Dictionary<Variable, VariableValue> CreateMutableValues(IReadOnlyDictionary<Variable, VariableValue> source)
        {
            var copy = new Dictionary<Variable, VariableValue>(source.Count);
            foreach (var kv in source)
            {
                copy[kv.Key] = kv.Value;
            }

            return copy;
        }

        internal static Dictionary<Variable, IReadOnlyList<ActiveModifier>> CreateMutableStacks(IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> source)
        {
            var copy = new Dictionary<Variable, IReadOnlyList<ActiveModifier>>(source.Count);
            foreach (var kv in source)
            {
                copy[kv.Key] = kv.Value;
            }

            return copy;
        }

        public EntityVariableState WithModifier(Variable variable, ActiveModifier modifier)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            var nextStacks = CreateMutableStacks(ModifierStacks);
            var nextBucket = BuildModifierBucketCopy(nextStacks, variable);
            int insertAt = ComputeModifierInsertIndex(nextBucket, modifier.Modifier.Order);
            nextBucket.Insert(insertAt, modifier);
            nextStacks[variable] = nextBucket;
            return this with { ModifierStacks = nextStacks };
        }

        public EntityVariableState WithoutModifier(Variable variable, ModifierId modifierId)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            if (!ModifierStacks.TryGetValue(variable, out IReadOnlyList<ActiveModifier>? existing) || existing == null)
            {
                return this;
            }

            int idx = IndexOfModifierInBucket(existing, modifierId);
            if (idx < 0)
            {
                return this;
            }

            return RemoveModifierAt(variable, existing, idx);
        }

        public EntityVariableState WithBaseValue(Variable variable, VariableValue value)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var nextBases = CreateMutableValues(BaseValues);
            nextBases[variable] = value;
            return this with { BaseValues = nextBases };
        }

        public EntityVariableState WithVariable(Variable variable, VariableValue initialValue)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            if (initialValue == null)
            {
                throw new ArgumentNullException(nameof(initialValue));
            }

            if (BaseValues.ContainsKey(variable))
            {
                return this;
            }

            var nextBases = CreateMutableValues(BaseValues);
            nextBases[variable] = initialValue;
            return this with { BaseValues = nextBases };
        }

        public IReadOnlyDictionary<Variable, VariableValue> ResolveEffectiveValues(IEntityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var keys = CollectKeysUnion(definition);
            return PopulateEffectiveFromKeys(keys, definition);
        }

        private HashSet<Variable> CollectKeysUnion(IEntityDefinition definition)
        {
            var keys = new HashSet<Variable>();
            foreach (var v in BaseValues.Keys)
            {
                keys.Add(v);
            }

            foreach (var v in ModifierStacks.Keys)
            {
                keys.Add(v);
            }

            foreach (var v in definition.DefinedVariables)
            {
                keys.Add(v);
            }

            return keys;
        }

        private Dictionary<Variable, VariableValue> PopulateEffectiveFromKeys(HashSet<Variable> keys, IEntityDefinition definition)
        {
            var result = new Dictionary<Variable, VariableValue>();
            foreach (Variable variable in keys)
            {
                TryAddFoldedEffective(variable, definition, result);
            }

            return result;
        }

        private void TryAddFoldedEffective(Variable variable, IEntityDefinition definition, Dictionary<Variable, VariableValue> result)
        {
            if (!ModifierStacks.TryGetValue(variable, out IReadOnlyList<ActiveModifier>? bucket) || bucket == null || bucket.Count == 0)
            {
                return;
            }

            VariableValue? baseValue = ResolveBaseValueForVariable(variable, definition);
            if (baseValue == null)
            {
                return;
            }

            result[variable] = baseValue.ApplyModifiers(bucket);
        }

        private VariableValue? ResolveBaseValueForVariable(Variable variable, IEntityDefinition definition)
        {
            if (BaseValues.TryGetValue(variable, out VariableValue? bv))
            {
                return bv;
            }

            if (definition.TryGetDefaultValue(variable, out VariableValue? dv))
            {
                return dv;
            }

            return null;
        }

        private EntityVariableState RemoveModifierAt(Variable variable, IReadOnlyList<ActiveModifier> bucket, int idx)
        {
            var nextStacks = CreateMutableStacks(ModifierStacks);
            var nextBucket = new List<ActiveModifier>(bucket);
            nextBucket.RemoveAt(idx);
            if (nextBucket.Count == 0)
            {
                nextStacks.Remove(variable);
            }
            else
            {
                nextStacks[variable] = nextBucket;
            }

            return this with { ModifierStacks = nextStacks };
        }

        private int IndexOfModifierInBucket(IReadOnlyList<ActiveModifier> bucket, ModifierId id)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Id.Equals(id))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<ActiveModifier> BuildModifierBucketCopy(Dictionary<Variable, IReadOnlyList<ActiveModifier>> stacks, Variable variable)
        {
            if (stacks.TryGetValue(variable, out IReadOnlyList<ActiveModifier>? existing) && existing != null)
            {
                return new List<ActiveModifier>(existing);
            }

            return new List<ActiveModifier>();
        }

        private int ComputeModifierInsertIndex(List<ActiveModifier> bucket, int order)
        {
            int insertAt = 0;
            while (insertAt < bucket.Count && bucket[insertAt].Modifier.Order <= order)
            {
                insertAt++;
            }

            return insertAt;
        }
    }
}
