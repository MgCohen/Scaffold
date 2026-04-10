using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [Serializable]
    public sealed class AttributeBag : IAttributeBag
    {
        public IAttributeBag Parent => parent;

        [NonSerialized] private IAttributeBag parent;

        public IReadOnlyList<AttributeEntry> Entries => entries;

        [SerializeField] private List<AttributeEntry> entries = new List<AttributeEntry>();

        public IEnumerable<Attribute> LocalKeys
        {
            get
            {
                EnsureCache();
                return localCache.Keys;
            }
        }

        [NonSerialized] private Dictionary<Attribute, AttributeValue> localCache;

        public event Action<Attribute, AttributeValue> OnAttributeAdded;
        public event Action<Attribute> OnAttributeRemoved;

        public void SetParent(IAttributeBag newParent)
        {
            parent = newParent;
        }

        public bool TryGetBase(Attribute key, out AttributeValue value)
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
                AttributeEntry entry = entries[i];
                if (entry == null || entry.Attribute == null || entry.BaseValue == null)
                {
                    continue;
                }

                Attribute entryKey = (Attribute)entry.Attribute;
                localCache[entryKey] = entry.BaseValue;
            }
        }

        internal void AddSerializedEntry(AttributeEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        public bool Add(Attribute key, AttributeValue initialBase)
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
            OnAttributeAdded?.Invoke(key, initialBase);
            return true;
        }

        public bool Remove(Attribute key)
        {
            EnsureCache();
            if (!localCache.Remove(key))
            {
                return false;
            }

            OnAttributeRemoved?.Invoke(key);
            return true;
        }

        internal void SetLocalSilent(Attribute key, AttributeValue value)
        {
            if (value == null)
            {
                return;
            }

            EnsureCache();
            localCache[key] = value;
        }

        internal bool RemoveLocalSilent(Attribute key)
        {
            EnsureCache();
            return localCache.Remove(key);
        }

        internal bool HasLocalKey(Attribute key)
        {
            EnsureCache();
            return localCache.ContainsKey(key);
        }

        private void EnsureCache()
        {
            if (localCache == null)
            {
                localCache = new Dictionary<Attribute, AttributeValue>();
            }
        }
    }
}
