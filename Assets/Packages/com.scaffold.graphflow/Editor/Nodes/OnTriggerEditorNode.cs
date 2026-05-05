#nullable enable
using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

// Concrete subclasses are emitted per package and carry [Serializable] themselves; this base
// also gets the attribute because Unity's [SerializeReference] walks the inheritance chain
// and warns on any unmarked link.

namespace Scaffold.GraphFlow.Editor.Nodes
{
    /// <summary>
    /// Generic base for the per-package OnTrigger editor mirror. The concrete subclass is
    /// emitted by the package generator and closes <typeparamref name="TEnum"/> over the
    /// per-package <c>&lt;Stem&gt;Catalog.EventType</c> enum, then forwards
    /// <see cref="GetPorts"/> to <c>&lt;Stem&gt;Catalog.Resolve(choice).Ports</c>.
    ///
    /// <para>Why generic + per-package shim instead of one shared class:</para>
    /// <list type="bullet">
    /// <item>GraphToolkit's <c>AddOption&lt;TEnum&gt;</c> needs a closed enum at compile time —
    /// the dropdown materializes from the enum values in metadata. Per-package enums give per-package
    /// dropdowns automatically.</item>
    /// <item><c>[UseWithGraph(typeof(&lt;Stem&gt;Graph))]</c> on the per-package shim scopes
    /// menu visibility to the right graph type without needing a shared <c>UseWithGraph</c> that
    /// would expose the node to every graph in the project.</item>
    /// <item>The base does all the work — the shim is a 5-line specialization, generator-emitted.</item>
    /// </list>
    ///
    /// <para>Ports come from a <see cref="PortMeta"/> list provided by the shim, which reads from
    /// the per-package generator-emitted catalog. No reflection on <see cref="Type.GetFields"/>,
    /// no <c>GraphEventTypeRegistry</c> indirection — the catalog is the source of truth and the
    /// shim is its only consumer here.</para>
    /// </summary>
    [Serializable]
    public abstract class OnTriggerEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string FlowOutPortName     = "FlowOut";
        public const string PayloadPortName     = "Payload";
        public const string EventTypeOptionName = "EventType";
        public const string TimingOptionName    = "Timing";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(EventTypeOptionName)
                .WithDisplayName("Event Type")
                .WithTooltip("Pick the event class this trigger subscribes to.");

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

            // Whole-payload output (D8) — exposes the OnTrigger<TEvent> wrapper instance for
            // graphs that want to thread the full payload through Modify/Decompose nodes
            // (Q-modify, deferred). Untyped here because TEvent isn't known until the picker
            // resolves; per-event field reads use the typed ports below.
            context.AddOutputPort(PayloadPortName).Build();

            var opt = GetNodeOptionByName(EventTypeOptionName);
            if (opt == null) return;
            if (!opt.TryGetValue<TEnum>(out var picked)) return;

            var ports = GetPorts(picked);
            if (ports == null) return;

            foreach (var p in ports)
            {
                if (p.Direction == PortDirection.Output)
                {
                    AddOutputByType(context, p.Name, p.Type);
                }
                else
                {
                    AddInputByType(context, p.Name, p.Type);
                }
            }
        }

        /// <summary>
        /// Provides the port shape for the picked event-type choice. Per-package shim implements
        /// this by reading from the catalog: <c>&lt;Stem&gt;Catalog.Resolve(picked)?.Ports</c>.
        /// </summary>
        protected abstract IReadOnlyList<PortMeta>? GetPorts(TEnum picked);

        static void AddOutputByType(IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))         context.AddOutputPort<int>(name).Build();
            else if (t == typeof(string)) context.AddOutputPort<string>(name).Build();
            else if (t == typeof(bool))   context.AddOutputPort<bool>(name).Build();
            else if (t == typeof(float))  context.AddOutputPort<float>(name).Build();
            else                          context.AddOutputPort(name).Build();
        }

        static void AddInputByType(IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))         context.AddInputPort<int>(name).Build();
            else if (t == typeof(string)) context.AddInputPort<string>(name).Build();
            else if (t == typeof(bool))   context.AddInputPort<bool>(name).Build();
            else if (t == typeof(float))  context.AddInputPort<float>(name).Build();
            else                          context.AddInputPort(name).Build();
        }
    }
}
