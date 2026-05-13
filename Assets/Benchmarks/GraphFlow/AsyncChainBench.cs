#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.GraphFlow
{
    /// <summary>
    /// Parameterized N-node async chain. Each node is a FlowInPort.Async whose
    /// handler awaits Task.CompletedTask — sync-completes, but goes through all
    /// the C# async lowering (state machine box + Task allocation per node fire).
    /// This isolates "how does per-Run allocation scale with N async nodes?"
    /// from any scheduling/ThreadPool overhead.
    ///
    /// Pre-built runners are stashed per N so we measure Run, not Build.
    /// </summary>
    public sealed class AsyncChainBench
    {
        static readonly int[] Sizes = { 1, 5, 10, 20, 50 };

        readonly Dictionary<int, PerfRunner> _runners = new();
        readonly EmptyPayload _payload = EmptyPayload.Instance;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new PerfBuilder();
            foreach (var n in Sizes)
                _runners[n] = builder.Build(BuildAsyncChain(n));
        }

        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        static PerfGraphAsset BuildAsyncChain(int n)
        {
            var nodes = new RuntimeNode[n + 1];
            nodes[0] = new EmptyEntry();
            for (int i = 0; i < n; i++) nodes[i + 1] = new AsyncPassNode();

            var asset = PerfAsset.Make(nodes);
            // Entry FlowOut → first pass node
            asset.Flow(nodes[0], "FlowOut", nodes[1], "In");
            // Chain pass1.Out → pass2.In → ... → passN.In
            for (int i = 1; i < n; i++)
                asset.Flow(nodes[i], "Out", nodes[i + 1], "In");
            return asset;
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(20)]
        [TestCase(50)]
        public void AsyncChain_Graph(int n)
        {
            var runner = _runners[n];
            // iterationsPer scaled inversely with N — keeps each measurement
            // batch in the ~ms range regardless of chain length.
            int iters = Math.Max(20, 500 / Math.Max(1, n));
            Bench.Measure(
                () => runner.Run(_payload).GetAwaiter().GetResult(),
                iterationsPer: iters,
                measurementId: $"AsyncChain_Graph_N{n}");
        }
    }

    [Serializable]
    public sealed class AsyncPassNode : RuntimeNode
    {
        public FlowInPort In = null!;
        public FlowOutPort Out = null!;

        public AsyncPassNode()
        {
            Out = new FlowOutPort(this, nameof(Out));
            In = FlowInPort.Async(this, nameof(In), async flow =>
            {
                // Sync-completing await — exercises the compiler-emitted state
                // machine + Task<FlowOutPort?> allocation per fire without
                // pulling in scheduling cost. Measures pure async overhead.
                await Task.CompletedTask;
                return Out;
            });
            Ports.Add(In.Name, In);
            Ports.Add(Out.Name, Out);
        }
    }
}
