#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class VariableBag : IVariableBag
    {
        public IVariableBag? Parent => parent;
        [NonSerialized] private IVariableBag? parent;

        internal IReadOnlyList<VariableEntry> Entries => entries;
        [SerializeField] private List<VariableEntry> entries = new List<VariableEntry>();

        public IEnumerable<Variable> LocalKeys => localCache.Keys;
        [NonSerialized] private Dictionary<Variable, VariableValue> localCache = new Dictionary<Variable, VariableValue>();

        public event Action<VariableStructuralChange, Variable, VariableValue?>? OnVariableStructuralChange;

        public void SetParent(IVariableBag? newParent)
        {
            parent = newParent;
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            if (localCache.TryGetValue(key, out value))
            {
                return true;
            }

            return parent != null && parent.TryGetBase(key, out value);
        }

        internal void AddSerializedEntry(VariableEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        internal void RebuildCache()
        {
            localCache.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                AddEntryToCacheIfValid(entries[i]);
            }
        }

        private void AddEntryToCacheIfValid(VariableEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Variable entryKey = entry.Key;
            if (string.IsNullOrEmpty(entryKey.Key) || entry.BaseValue == null)
            {
                return;
            }

            localCache[entryKey] = entry.BaseValue;
        }

        public bool Add(Variable key, VariableValue initialBase)
        {
            if (initialBase == null)
            {
                return false;
            }

            if (localCache.ContainsKey(key))
            {
                return false;
            }

            localCache[key] = initialBase;
            OnVariableStructuralChange?.Invoke(VariableStructuralChange.Added, key, initialBase);
            return true;
        }

        public bool Remove(Variable key)
        {
            if (!localCache.Remove(key))
            {
                return false;
            }

            OnVariableStructuralChange?.Invoke(VariableStructuralChange.Removed, key, null);
            return true;
        }

        internal void SetLocalSilent(Variable key, VariableValue value)
        {
            if (value == null)
            {
                return;
            }

            localCache[key] = value;
        }

        internal bool RemoveLocalSilent(Variable key)
        {
            return localCache.Remove(key);
        }

        internal bool HasLocalKey(Variable key)
        {
            return localCache.ContainsKey(key);
        }
    }
}
