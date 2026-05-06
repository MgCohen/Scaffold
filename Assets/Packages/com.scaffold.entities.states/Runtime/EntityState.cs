#nullable enable

using System;
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed record EntityState(IReadOnlyDictionary<Variable, VariableValue> BaseValues, IReadOnlyDictionary<Variable, IReadOnlyList<ActiveModifier>> ModifierStacks) : State
    {
        public static EntityState Empty { get; } = new EntityState(new Dictionary<Variable, VariableValue>(), new Dictionary<Variable, IReadOnlyList<ActiveModifier>>());

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

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            if (BaseValues.TryGetValue(key, out var bv) && bv != null)
            {
                value = bv;
                return true;
            }
            value = default!;
            return false;
        }

        public IEnumerable<ActiveModifier> GetModifiers(Variable key)
        {
            if (ModifierStacks.TryGetValue(key, out var bucket) && bucket != null)
                return bucket;
            return Array.Empty<ActiveModifier>();
        }

        public IEnumerable<Variable> Variables
        {
            get
            {
                var keys = new HashSet<Variable>(BaseValues.Keys);
                foreach (var k in ModifierStacks.Keys) keys.Add(k);
                return keys;
            }
        }

        public EntityState WithModifier(Variable variable, ActiveModifier modifier)
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

        public EntityState WithoutModifier(Variable variable, ModifierId modifierId)
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

        public EntityState WithBaseValue(Variable variable, VariableValue value)
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

        public EntityState WithVariable(Variable variable, VariableValue initialValue)
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

        public EntityState WithoutVariable(Variable variable)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }

            if (!VariableHasRuntimeData(variable))
            {
                return this;
            }

            return BuildWithoutVariable(variable);
        }

        private bool VariableHasRuntimeData(Variable variable)
        {
            return BaseValues.ContainsKey(variable) || ModifierStacks.ContainsKey(variable);
        }

        private EntityState BuildWithoutVariable(Variable variable)
        {
            Dictionary<Variable, VariableValue>? nextBases = BaseValues.ContainsKey(variable) ? CreateMutableValues(BaseValues) : null;
            if (nextBases != null)
            {
                nextBases.Remove(variable);
            }

            Dictionary<Variable, IReadOnlyList<ActiveModifier>>? nextStacks = ModifierStacks.ContainsKey(variable) ? CreateMutableStacks(ModifierStacks) : null;
            if (nextStacks != null)
            {
                nextStacks.Remove(variable);
            }

            return this with
            {
                BaseValues = nextBases ?? BaseValues,
                ModifierStacks = nextStacks ?? ModifierStacks
            };
        }

        public EntityState WithoutModifiersFromSource(ModifierSource source)
        {
            var nextStacks = CreateMutableStacks(ModifierStacks);
            if (!BuildStrippedModifierStacks(nextStacks, source))
            {
                return this;
            }

            return this with { ModifierStacks = nextStacks };
        }

        public EntityState WithoutBase(Variable key)
        {
            if (!BaseValues.ContainsKey(key)) return this;
            var next = CreateMutableValues(BaseValues);
            next.Remove(key);
            return this with { BaseValues = next };
        }

        public EntityState WithoutAllModifiers()
            => this with { ModifierStacks = new Dictionary<Variable, IReadOnlyList<ActiveModifier>>() };

        private bool BuildStrippedModifierStacks(Dictionary<Variable, IReadOnlyList<ActiveModifier>> nextStacks, ModifierSource source)
        {
            bool changed = false;
            var keysToCheck = new List<Variable>(nextStacks.Keys);
            foreach (Variable v in keysToCheck)
            {
                if (TryRebuildStackWithoutSource(nextStacks, v, source))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private bool TryRebuildStackWithoutSource(Dictionary<Variable, IReadOnlyList<ActiveModifier>> nextStacks, Variable variable, ModifierSource source)
        {
            IReadOnlyList<ActiveModifier> bucket = nextStacks[variable];
            if (!TryBuildBucketWithoutSource(bucket, source, out List<ActiveModifier>? rebuilt))
            {
                return false;
            }

            ApplyRebuiltBucket(nextStacks, variable, rebuilt);
            return true;
        }

        private bool TryBuildBucketWithoutSource(IReadOnlyList<ActiveModifier> bucket, ModifierSource source, out List<ActiveModifier>? rebuilt)
        {
            rebuilt = null;
            for (int i = 0; i < bucket.Count; i++)
            {
                ApplyBucketIndexForSourceStrip(bucket, i, source, ref rebuilt);
            }

            return rebuilt != null;
        }

        private void ApplyBucketIndexForSourceStrip(IReadOnlyList<ActiveModifier> bucket, int i, ModifierSource source, ref List<ActiveModifier>? rebuilt)
        {
            ActiveModifier am = bucket[i];
            bool keep = !(am.Source.HasValue && am.Source.Value.Equals(source));
            if (rebuilt == null && !keep)
            {
                rebuilt = new List<ActiveModifier>(bucket.Count);
                CopyBucketPrefix(bucket, rebuilt, i);
                return;
            }

            if (rebuilt != null && keep)
            {
                rebuilt.Add(am);
            }
        }

        private void CopyBucketPrefix(IReadOnlyList<ActiveModifier> bucket, List<ActiveModifier> rebuilt, int exclusiveEnd)
        {
            for (int j = 0; j < exclusiveEnd; j++)
            {
                rebuilt.Add(bucket[j]);
            }
        }

        private void ApplyRebuiltBucket(Dictionary<Variable, IReadOnlyList<ActiveModifier>> nextStacks, Variable variable, List<ActiveModifier> rebuilt)
        {
            if (rebuilt.Count == 0)
            {
                nextStacks.Remove(variable);
            }
            else
            {
                nextStacks[variable] = rebuilt;
            }
        }

        private EntityState RemoveModifierAt(Variable variable, IReadOnlyList<ActiveModifier> bucket, int idx)
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
