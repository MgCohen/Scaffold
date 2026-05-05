using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid = string.Empty;

        [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

        public virtual Task Execute(Flow flow) => Task.CompletedTask;

        public Connection Bind(string dstPortName, RuntimeNode src, string srcPortName)
        {
            if (!Ports.TryGetValue(dstPortName, out var dstPort))
                throw new ArgumentException($"Destination node has no port named '{dstPortName}'.");
            if (!src.Ports.TryGetValue(srcPortName, out var srcPort))
                throw new ArgumentException($"Source node has no port named '{srcPortName}'.");

            return Connection.Bind(dstPort, srcPort);
        }
    }

    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
#nullable enable
        [NonSerialized] TRunner? _runner;
#nullable disable

        internal void BindRunner(TRunner runner) => _runner = runner;

        public sealed override Task Execute(Flow flow) => Execute(_runner, flow);
        public abstract Task Execute(TRunner runner, Flow flow);
    }
}
