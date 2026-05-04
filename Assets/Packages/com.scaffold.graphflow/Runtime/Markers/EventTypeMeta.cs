#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Compile-time port-field record for an event class — one per public instance field on the
    /// event. Emitted into per-package <c>&lt;Stem&gt;GraphRegistry.EventTypes</c> at generator time
    /// and read by <c>OnTriggerEditorNode</c> to define dynamic output ports.
    /// </summary>
    public sealed class EventPortMeta
    {
        public string Name { get; }
        public Type Type { get; }
        public EventPortMeta(string name, Type type) { Name = name; Type = type; }
    }

    /// <summary>
    /// One entry in a per-package <c>EventTypes</c> table — pairs an event <see cref="System.Type"/>
    /// with the metadata for its public instance fields. Generator-emitted at compile time;
    /// <c>OnTriggerEditorNode</c> reads the union of all packages' tables (via
    /// <see cref="GraphEventTypeRegistry"/>) to populate its event-type dropdown and define dynamic
    /// output ports.
    /// </summary>
    public sealed class EventTypeMeta
    {
        public Type Type { get; }
        public IReadOnlyList<EventPortMeta> PortFields { get; }
        public EventTypeMeta(Type type, IReadOnlyList<EventPortMeta> portFields)
        {
            Type = type;
            PortFields = portFields;
        }
    }

    /// <summary>
    /// Process-wide registry of <c>[GraphEvent]</c>-tagged types — populated by per-package
    /// generated registries calling <see cref="Register"/> at module-load time. The package's
    /// editor mirror (<c>OnTriggerEditorNode</c>) reads this to populate its dropdown and define
    /// dynamic output ports for the picked event type.
    /// </summary>
    public static class GraphEventTypeRegistry
    {
        static readonly Dictionary<Type, EventTypeMeta> s_byType = new Dictionary<Type, EventTypeMeta>();
        static readonly List<EventTypeMeta> s_all = new List<EventTypeMeta>();

        public static void Register(EventTypeMeta meta)
        {
            if (meta == null) return;
            if (s_byType.ContainsKey(meta.Type)) return;
            s_byType[meta.Type] = meta;
            s_all.Add(meta);
        }

        public static IReadOnlyList<EventTypeMeta> All => s_all;

        public static EventTypeMeta? Get(Type eventType) =>
            s_byType.TryGetValue(eventType, out var m) ? m : null;
    }
}
