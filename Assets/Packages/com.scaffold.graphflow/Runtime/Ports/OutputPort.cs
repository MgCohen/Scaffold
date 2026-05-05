#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        readonly Func<Flow, T> _compute;
        readonly Dictionary<Flow, T> _cache = new();
        readonly bool _shouldCache;

        public OutputPort(Func<Flow, T> compute, bool cache = true)
        {
            _compute = compute;
            _shouldCache = cache;
        }

        public T Read(Flow flow)
        {
            if (!_shouldCache) return _compute(flow);
            if (_cache.TryGetValue(flow, out var v)) return v;
            v = _compute(flow);
            _cache[flow] = v;
            flow.RegisterTouched(this);
            return v;
        }

        internal override void ClearCache(Flow flow) => _cache.Remove(flow);
    }
}
