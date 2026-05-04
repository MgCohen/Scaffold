#nullable enable
using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.Nodes
{
    /// <summary>
    /// Built-in trigger entry editor mirror (post-M3 decision #3 — single non-generic node serves
    /// every event type). Uses GraphToolkit's <c>OnDefineOptions</c> + <c>OnDefinePorts</c> to
    /// dynamically expose:
    /// <list type="bullet">
    /// <item>An <c>EventType</c> dropdown — string of the picked event type's assembly-qualified
    /// name. Sourced from <see cref="GraphEventTypeRegistry"/>'s union of per-package
    /// <c>EventTypes</c> tables.</item>
    /// <item>A <see cref="Timing"/> dropdown for Before / After.</item>
    /// </list>
    /// <para>OnDefinePorts re-runs whenever an option changes; the per-event public-field set is
    /// projected as one typed output port per field on the picked event class.</para>
    /// <para>The runtime <c>OnTrigger&lt;TEvent&gt;</c> closed over the picked TEvent is constructed
    /// reflectively at hydration time (one-time, not hot path) by the Factory delegate the
    /// generated registry registers under this editor type.</para>
    /// <para>Editor-option choice rationale: tier-2 (string AQN) per the brief's fallback ladder.
    /// GraphToolkit's <c>IOptionDefinitionContext.AddOption&lt;T&gt;</c> requires <c>T</c> to be a
    /// type GraphToolkit can serialize; for portability across assets and project versions, the
    /// assembly-qualified type name is the safest persistent representation. UI is a plain text
    /// field — proper picker UX would require either Scaffold.Types' <c>TypeReference</c> with
    /// custom GT serialization support or a per-package generated enum.</para>
    /// </summary>
    [Serializable]
    public sealed class OnTriggerEditorNode : Node
    {
        public const string FlowOutPortName     = "FlowOut";
        public const string EventTypeOptionName = "EventType";
        public const string TimingOptionName    = "Timing";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(EventTypeOptionName)
                .WithDisplayName("Event Type (AQN)")
                .WithTooltip("Assembly-qualified name of the [GraphEvent]-tagged class to trigger on.")
                .WithDefaultValue(string.Empty);

            context.AddOption<Timing>(TimingOptionName)
                .WithDisplayName("Timing")
                .WithTooltip("Before — trigger runs before the event's surrounding action; After — trigger runs after.")
                .WithDefaultValue(Timing.Before);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort(FlowOutPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            var typeOpt = GetNodeOptionByName(EventTypeOptionName);
            if (typeOpt == null) return;
            if (!typeOpt.TryGetValue<string>(out var aqn) || string.IsNullOrEmpty(aqn)) return;

            var resolved = Type.GetType(aqn, throwOnError: false);
            if (resolved == null) return;

            var meta = GraphEventTypeRegistry.Get(resolved);
            if (meta == null) return;

            foreach (var f in meta.PortFields)
            {
                // Best-effort: emit a typed port for primitives we know how to map; fall back to
                // an untyped reference for everything else (object?). Matches the per-event
                // generator's port-emit behavior (TypeFmt.Simple) so the registry stays honest.
                if (f.Type == typeof(int))
                    context.AddOutputPort<int>(f.Name).Build();
                else if (f.Type == typeof(string))
                    context.AddOutputPort<string>(f.Name).Build();
                else if (f.Type == typeof(bool))
                    context.AddOutputPort<bool>(f.Name).Build();
                else if (f.Type == typeof(float))
                    context.AddOutputPort<float>(f.Name).Build();
                else
                    context.AddOutputPort(f.Name).Build();
            }
        }
    }
}
