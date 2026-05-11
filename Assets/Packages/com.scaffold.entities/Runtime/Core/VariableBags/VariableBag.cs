#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed partial class VariableBag : IVariableBag
    {
        IVariableBag? IVariableBag.Parent => parent;
        [NonSerialized] private VariableBag? parent;

        internal IReadOnlyList<VariableEntry> Entries => entries;
        [SerializeField] private List<VariableEntry> entries = new List<VariableEntry>();

        public IEnumerable<Variable> LocalKeys => localCache.Keys;
        [NonSerialized] private Dictionary<Variable, VariableValue> localCache = new Dictionary<Variable, VariableValue>();

        public event Action<VariableStructuralChange, Variable, VariableValue?>? OnVariableStructuralChange;

        public void SetParent(VariableBag? newParent)
        {
            parent = newParent;
        }

        public bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle)
        {
            var lookupKey = new Variable(id, "");
            if (localCache.TryGetValue(lookupKey, out var val) && val is VariableValue<T>)
            {
                handle = new EntityVariableHandle<T>(id,
                    () => localCache.TryGetValue(lookupKey, out var v) && v is IVariableValue<T> t ? t.Get() : default!,
                    newVal =>
                    {
                        if (localCache.TryGetValue(lookupKey, out var ex) && ex is VariableValue<T> typed)
                            localCache[lookupKey] = typed.CreateWithValue(newVal);
                    });
                return true;
            }
            if (parent != null)
                return ((IVariableBag)parent).TryGet(id, out handle);
            handle = null;
            return false;
        }

        public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
        {
            var lookupKey = new Variable(id, "");
            if (localCache.TryGetValue(lookupKey, out var val) && val != null)
            {
                handle = new EntityVariableHandle(id, EntityVariableHandle.ResolvePayloadType(val));
                return true;
            }
            if (parent != null)
                return ((IVariableBag)parent).TryGet(id, out handle);
            handle = null;
            return false;
        }

        public IEnumerable<IVariableHandle> LocalHandles
        {
            get
            {
                foreach (var kvp in localCache)
                {
                    if (kvp.Value != null)
                        yield return new EntityVariableHandle(kvp.Key.Id, EntityVariableHandle.ResolvePayloadType(kvp.Value));
                }
            }
        }

        [Obsolete("Use TryGet<T> instead. Will be removed in a future release.")]
        public bool TryGetBase(Variable key, out VariableValue value)
        {
            return TryGetBaseCore(key, out value);
        }

        private bool TryGetBaseCore(Variable key, out VariableValue value)
        {
            if (localCache.TryGetValue(key, out value))
            {
                return true;
            }

            return parent != null && parent.TryGetBaseCore(key, out value);
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
            if (string.IsNullOrEmpty(entryKey.Id) || entry.BaseValue == null)
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
