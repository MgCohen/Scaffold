#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        // Paired version-stamp + value, one slot per flow index. An entry is
        // fresh only when its Version matches the current flow.CacheVersion;
        // anything else is a miss and triggers recompute.
        internal struct Entry
        {
            public int Version;
            public T Value;
        }

        Entry[] _cache = Array.Empty<Entry>();
        readonly Func<Flow, T> _compute;
        readonly bool _shouldCache;

        public OutputPort(Func<Flow, T> compute, bool cache = true)
        {
            _compute = compute;
            _shouldCache = cache;
        }

        internal override void Bake(int maxFlows)
        {
            if (!_shouldCache) return;
            if (_cache.Length < maxFlows)
                _cache = new Entry[maxFlows];
        }

        public T Read(Flow flow)
        {
            if (!_shouldCache) return _compute(flow);

            ref var e = ref _cache[flow.Index];
            if (e.Version == flow.CacheVersion) return e.Value;
            e.Value = _compute(flow);
            e.Version = flow.CacheVersion;
            return e.Value;
        }
    }
}
