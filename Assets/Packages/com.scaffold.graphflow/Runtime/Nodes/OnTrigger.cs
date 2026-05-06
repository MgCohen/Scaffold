#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<TEvent>, IOnTrigger
        where TEvent : class
    {
        // Bake-time only. The package generator's registry factory writes Timing once
        // when the editor option is materialized into the runtime node. Never mutated
        // during a Run — treating this as per-run state would race across concurrent flows.
        public Timing Timing { get; set; }

        public FlowOutPort FlowOut = null!;
        public OutputPort<TEvent> Event = null!;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Event = new OutputPort<TEvent>(flow => flow.GetPayload<TEvent>()!);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Event), Event);
        }
    }
}
