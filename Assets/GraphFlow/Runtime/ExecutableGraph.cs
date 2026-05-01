using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class ExecutableGraph
    {
        public ExecutableGraph(
            IReadOnlyList<ExecutableNode> nodes,
            IReadOnlyDictionary<Type, ExecutableNode> entryRoots)
        {
            Nodes = nodes;
            EntryRoots = entryRoots;
        }

        public IReadOnlyList<ExecutableNode> Nodes { get; }
        public IReadOnlyDictionary<Type, ExecutableNode> EntryRoots { get; }
    }
}
