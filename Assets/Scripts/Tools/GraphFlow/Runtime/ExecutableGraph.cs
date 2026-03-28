using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class ExecutableGraph
    {
        public ExecutableGraph(
            IReadOnlyList<ExecutableNode> nodes,
            IReadOnlyDictionary<Type, ExecutableNode> entryRoots,
            IReadOnlyList<ReactiveHookRuntime> reactiveHooks)
        {
            Nodes = nodes;
            EntryRoots = entryRoots;
            ReactiveHooks = reactiveHooks;
        }

        public IReadOnlyList<ExecutableNode> Nodes { get; }
        public IReadOnlyDictionary<Type, ExecutableNode> EntryRoots { get; }
        public IReadOnlyList<ReactiveHookRuntime> ReactiveHooks { get; }
    }
}
