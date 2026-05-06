using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Spike.Editor
{
    /// <summary>
    /// Phase 2.5 spike — simulates the per-package generated shape we'd emit in production for
    /// OnTrigger / Return type pickers.
    /// <para>The full pattern, hand-written here for validation:</para>
    /// <list type="number">
    /// <item>An enum, one entry per <c>[GraphEvent]</c>-tagged class in the package
    /// (here: <see cref="SpikeEventChoice"/> covers two stand-in event classes).</item>
    /// <item><c>AddOption&lt;TEnum&gt;</c> — GT-native dropdown, refresh, persistence — proven in Phase 1.</item>
    /// <item>A hand-coded switch maps the enum value to a <see cref="System.Type"/> (the generator
    /// would emit this from the same discovery rule that produced the enum).</item>
    /// <item><c>OnDefinePorts</c> reads the mapped Type, emits one typed port per public field —
    /// matches the dynamic-port emit shape OnTrigger needs.</item>
    /// </list>
    /// <para>If this works end-to-end, the generator change becomes mechanical: emit enum + switch
    /// per package, swap the OnTrigger editor mirror to use them.</para>
    /// </summary>
    [Serializable]
    public sealed class SpikeEnumTypeNode : Node
    {
        const string EventOptionName = "EventType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<SpikeEventChoice>(EventOptionName)
                .WithDisplayName("Event Type")
                .WithTooltip("Phase 2.5 — enum-based type pick. Stands in for the generator-emitted per-package event-type enum.")
                .WithDefaultValue(SpikeEventChoice.None);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // Always emit a flow-style output so the node has a stable visual identity.
            context.AddOutputPort<int>("DummyOut").Build();

            var pickedType = ResolveEventType();
            if (pickedType == null) return;

            // Dynamic per-field output ports — same shape OnTrigger will use to expose event
            // fields downstream.
            foreach (var f in pickedType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.FieldType == typeof(int))
                    context.AddOutputPort<int>(f.Name).Build();
                else if (f.FieldType == typeof(string))
                    context.AddOutputPort<string>(f.Name).Build();
                else if (f.FieldType == typeof(bool))
                    context.AddOutputPort<bool>(f.Name).Build();
                else
                    context.AddOutputPort(f.Name).Build();
            }
        }

        /// <summary>
        /// Generator-emitted-equivalent: maps each enum value to its concrete CLR type. In the
        /// production generator this would come from the same <c>[GraphEvent]</c> discovery walk
        /// that produces the enum itself, so the two stay in lockstep automatically.
        /// </summary>
        Type? ResolveEventType()
        {
            var opt = GetNodeOptionByName(EventOptionName);
            if (opt == null) return null;
            if (!opt.TryGetValue<SpikeEventChoice>(out var choice)) return null;

            return choice switch
            {
                SpikeEventChoice.SpikeDamage => typeof(SpikeDamage),
                SpikeEventChoice.SpikeHeal   => typeof(SpikeHeal),
                _ => null,
            };
        }
    }

    /// <summary>
    /// Stand-in for the generator-emitted per-package enum. In production this would be
    /// <c>&lt;Stem&gt;EventTypeChoice</c> — one entry per <c>[GraphEvent]</c>-tagged class in the
    /// package's runtime asm, plus a <c>None</c> sentinel.
    /// <para>The explicit numeric values are deliberate (cf. our string-port-id stability story):
    /// adding/removing event types in the future only invalidates affected enum values, not all of
    /// them. A full production implementation would derive these from a stable hash of the type
    /// name to survive reorderings.</para>
    /// </summary>
    public enum SpikeEventChoice
    {
        None        = 0,
        SpikeDamage = 1,
        SpikeHeal   = 2,
    }
}
