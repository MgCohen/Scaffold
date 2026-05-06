#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class BakedGraph
    {
        public IReadOnlyList<RuntimeNode> Nodes { get; }
        public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }

        internal BakedGraph(
            IReadOnlyList<RuntimeNode> nodes,
            IReadOnlyDictionary<Type, EntryRuntimeNodeBase> entries)
        {
            Nodes = nodes;
            EntriesByPayload = entries;
        }
    }
}
