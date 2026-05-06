#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class FlowInPort : Port
    {
        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }
        internal Func<Flow, ValueTask<FlowOutPort?>> Invoke { get; }

        FlowInPort(RuntimeNode owner, string name, Func<Flow, ValueTask<FlowOutPort?>> invoke)
        {
            Owner = owner;
            Name = name;
            Invoke = invoke;
        }

        public static FlowInPort Sync(
            RuntimeNode owner, string name, Func<Flow, FlowOutPort?> handler) =>
            new(owner, name, flow => new ValueTask<FlowOutPort?>(handler(flow)));

        public static FlowInPort Async(
            RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort?>> handler) =>
            new(owner, name, flow => new ValueTask<FlowOutPort?>(handler(flow)));
    }
}
