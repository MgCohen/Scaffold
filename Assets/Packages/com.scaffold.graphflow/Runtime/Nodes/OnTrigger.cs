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
            // Copy the per-event payload's Event onto the asset-instance node so the body of the
            // flow can read .Event through this typed entry reference. The asset-instance Timing
            // is what hosts read at wiring; per-event Payload.Timing is telemetry only.
            if (Payload != null) Event = Payload.Event;
            return flow.GoTo(FlowOut);
        }
    }
}
