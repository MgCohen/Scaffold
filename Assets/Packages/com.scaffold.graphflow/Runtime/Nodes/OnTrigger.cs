#nullable enable
using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Built-in trigger entry primitive (post-M3 decision #3). Single editor mirror
    /// (<c>OnTriggerEditorNode</c>) drives all event types — the picked event class +
    /// <see cref="Timing"/> are configured via dynamic options; the closed
    /// <c>OnTrigger&lt;TEvent&gt;</c> instance is constructed reflectively at hydration based on
    /// the picked type.
    ///
    /// <para>The runtime node IS its own payload — hosts pattern-match
    /// <c>controller.EntryNodes</c> for <c>OnTrigger&lt;DamageDealt&gt;</c>, read the configured
    /// <see cref="Timing"/> field, and subscribe to their bus accordingly. On each event delivery
    /// the host constructs a fresh <c>OnTrigger&lt;DamageDealt&gt; { Event = e, Timing = ... }</c>
    /// and passes it to <see cref="EntryRuntimeNode{TEntry}.Run"/>; that becomes the per-run
    /// <c>Payload</c> the flow body reads (via <c>Payload.Event</c>).</para>
    /// </summary>
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<OnTrigger<TEvent>>, IOnTrigger
        where TEvent : class
    {
        /// <summary>The event reference. Set by the host on the per-event payload passed to
        /// <see cref="EntryRuntimeNode{TEntry}.Run"/>; the configured asset-instance leaves it
        /// null.</summary>
        public TEvent? Event;

        /// <summary>Trigger phase. On the asset-instance node this is the edit-time choice the host
        /// reads at wiring; on the per-event payload it's whatever phase the host is delivering.
        /// Exposed through <see cref="IOnTrigger"/> so cross-package callers can apply the
        /// edit-time choice without knowing <typeparamref name="TEvent"/>.</summary>
        public Timing Timing { get; set; }

        /// <summary>Single flow exit — drives the body of the trigger graph.</summary>
        public FlowOutPort FlowOut = null!;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Ports.Add(FlowOut.Name, FlowOut);
        }

        public override Task Execute(Flow flow)
        {
            // Copy the per-event payload's Event reference onto this asset-instance node so
            // downstream nodes that hold a reference to the trigger (the typed entry) can read
            // .Event during the flow walk. The configured Timing on the asset-instance is what
            // hosts read at wiring; the per-event Payload.Timing is for telemetry only.
            if (Payload != null) Event = Payload.Event;
            return flow.GoTo(FlowOut);
        }
    }
}
