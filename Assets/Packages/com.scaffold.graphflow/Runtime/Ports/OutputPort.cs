#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    public sealed class OutputPort<T> : Port
    {
        readonly Func<Flow, T> _compute;
        readonly bool _shouldCache;

        public OutputPort(Func<Flow, T> compute, bool cache = true)
        {
            _compute = compute;
            _shouldCache = cache;
        }

        public T Read(Flow flow) =>
            _shouldCache ? flow.ReadCached(this, _compute) : _compute(flow);
    }
}
