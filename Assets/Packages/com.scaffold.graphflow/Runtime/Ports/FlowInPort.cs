#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class FlowInPort : Port
    {
        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }

        // Exactly one of these is set; Sync() / Async() factories enforce that.
        // Branching once on the field is cheaper than wrapping the user's sync
        // handler in a flow=>ValueTask(handler(flow)) closure on every fire —
        // that wrap previously cost an extra delegate invocation per node call.
        readonly Func<Flow, FlowOutPort?>? _sync;
        readonly Func<Flow, Task<FlowOutPort?>>? _async;

        FlowInPort(RuntimeNode owner, string name,
                   Func<Flow, FlowOutPort?>? sync,
                   Func<Flow, Task<FlowOutPort?>>? @async)
        {
            Owner = owner;
            Name = name;
            _sync = sync;
            _async = @async;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTask<FlowOutPort?> Invoke(Flow flow) =>
            _sync != null
                ? new ValueTask<FlowOutPort?>(_sync(flow))
                : new ValueTask<FlowOutPort?>(_async!(flow));

        public static FlowInPort Sync(
            RuntimeNode owner, string name, Func<Flow, FlowOutPort?> handler) =>
            new(owner, name, handler, null);

        public static FlowInPort Async(
            RuntimeNode owner, string name, Func<Flow, Task<FlowOutPort?>> handler) =>
            new(owner, name, null, handler);
    }
}
