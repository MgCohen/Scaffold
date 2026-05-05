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
        internal Func<Flow, Task<FlowOutPort>> Invoke { get; }

        FlowInPort(RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort>> invoke)
        {
            Owner = owner;
            Name = name;
            Invoke = invoke;
        }

        public static FlowInPort Sync(
            RuntimeNode owner, string name, Func<Flow, FlowOutPort> handler) =>
            new(owner, name, flow => Task.FromResult(handler(flow)));

        public static FlowInPort Async(
            RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort>> handler) =>
            new(owner, name, handler);
    }
}
