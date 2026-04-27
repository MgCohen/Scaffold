using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class VariableBag : IVariableBag
    {
        public IVariableBag Parent => parent;

        [NonSerialized] private IVariableBag parent;

        internal IReadOnlyList<VariableEntry> Entries => entries;

        [SerializeField] private List<VariableEntry> entries = new List<VariableEntry>();

        public IEnumerable<Variable> LocalKeys
        {
            get
            {
                EnsureCache();
                return localCache.Keys;
            }
        }

        [NonSerialized] private Dictionary<Variable, VariableValue> localCache;

        public event Action<Variable, VariableValue> OnVariableAdded;
        public event Action<Variable> OnVariableRemoved;

        public void SetParent(IVariableBag newParent)
        {
            parent = newParent;
        }

        public bool TryGetBase(Variable key, out VariableValue value)
        {
            EnsureCache();
            if (localCache.TryGetValue(key, out value))
            {
                return true;
            }

            return parent != null && parent.TryGetBase(key, out value);
        }

        internal void RebuildCache()
        {
            EnsureCache();
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

        internal void AddSerializedEntry(VariableEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

#if UNITY_EDITOR
        internal void EditorApplyVariableAuthoringFromValidation()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                VariableEntry entry = entries[i];
                entry?.EditorApplyAuthoringIntoInlineSerializedKeyAndClearLegacy();
                entry?.RebaseSerializedPayloadIfMismatch();
            }
        }
#endif

        public bool Add(Variable key, VariableValue initialBase)
        {
            if (initialBase == null)
            {
                return false;
            }

            EnsureCache();
            if (localCache.ContainsKey(key))
            {
                return false;
            }

            localCache[key] = initialBase;
            OnVariableAdded?.Invoke(key, initialBase);
            return true;
        }

        public bool Remove(Variable key)
        {
            EnsureCache();
            if (!localCache.Remove(key))
            {
                return false;
            }

            OnVariableRemoved?.Invoke(key);
            return true;
        }

        internal void SetLocalSilent(Variable key, VariableValue value)
        {
            if (value == null)
            {
                return;
            }

            EnsureCache();
            localCache[key] = value;
        }

        internal bool RemoveLocalSilent(Variable key)
        {
            EnsureCache();
            return localCache.Remove(key);
        }

        internal bool HasLocalKey(Variable key)
        {
            EnsureCache();
            return localCache.ContainsKey(key);
        }

        private void EnsureCache()
        {
            if (localCache == null)
            {
                localCache = new Dictionary<Variable, VariableValue>();
            }
        }
    }
}
