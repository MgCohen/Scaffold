#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        // Paired version-stamp + value per flow index. Sized to the runner's
        // MaxConcurrentFlows at Bake time. Step 2 allocates this; Step 3 will
        // switch Read() to consult it. Until then, the dict-based Flow._cache
        // remains the live cache (covered by ReadCached below).
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

            // Defensive grow for ports created post-Build with cache: true that
            // weren't Baked. Steady state never hits this branch.
            if (_cache.Length <= flow.Index)
            {
                int target = flow.Runner.MaxConcurrentFlows;
                if (target <= flow.Index) target = flow.Index + 1;
                var grown = new Entry[target];
                Array.Copy(_cache, grown, _cache.Length);
                _cache = grown;
            }

            ref var e = ref _cache[flow.Index];
            if (e.Version == flow.CacheVersion) return e.Value;
            e.Value = _compute(flow);
            e.Version = flow.CacheVersion;
            return e.Value;
        }
    }
}
