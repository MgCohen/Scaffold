#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>, IOnTrigger
        where TEvent : class
    {
        public Timing Timing { get; set; }

        public TEvent? Inner;

        public FlowOutPort FlowOut;
        public OutputPort<TEvent> Event;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Event = new OutputPort<TEvent>(
                flow => flow.GetPayload<OnTrigger<TEvent>>()!.Inner!);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Event), Event);
        }
    }
}
