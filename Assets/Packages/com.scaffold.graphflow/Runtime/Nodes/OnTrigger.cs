#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>, IOnTrigger
        where TEvent : class
    {
        public TEvent? Event;
        public Timing Timing { get; set; }
        public FlowOutPort FlowOut = null!;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowOut.Name, FlowOut);
        }

        public override Task Execute(Flow flow)
        {
            if (Payload != null) Event = Payload.Event;
            return flow.GoTo(FlowOut);
        }
    }
}
