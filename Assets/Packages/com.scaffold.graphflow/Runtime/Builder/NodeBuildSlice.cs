#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public readonly struct NodeBuildSlice
    {
        public IReadOnlyList<DataBinding> Data { get; }
        public IReadOnlyList<FlowBinding> Flow { get; }

        public NodeBuildSlice(IReadOnlyList<DataBinding> data,
                              IReadOnlyList<FlowBinding> flow)
        {
            Data = data;
            Flow = flow;
        }
    }

    public readonly struct DataBinding
    {
        readonly Action _apply;
        public DataBinding(Action apply) { _apply = apply; }
        public void Apply() => _apply();
    }

    public readonly struct FlowBinding
    {
        public FlowOutPort Source { get; }
        public FlowInPort Destination { get; }
        public FlowConnection Connection { get; }

        public FlowBinding(FlowOutPort src, FlowInPort dst, FlowConnection conn)
        {
            Source = src;
            Destination = dst;
            Connection = conn;
        }
    }
}
