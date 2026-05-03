using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;

namespace Scaffold.Benchmarks.Maps
{
    /// <summary>
    /// Validates the harness's allocation signals: paths that must allocate report &gt; 0 here;
    /// warmed hot paths that should not allocate use <see cref="Bench.NoAllocations"/>.
    /// Byte assertions are skipped when no byte counter advances on this runtime; the
    /// <c>GC.Alloc</c> Profiler marker count is checked instead — that signal works in EditMode
    /// regardless of Mono's per-thread heap-counter quirks.
    /// </summary>
    public sealed class PerformanceAllocationVerificationTests
    {
        static readonly Lazy<bool> BytesCounterWorks = new(() => Bench.BytesCounterWorks);
        static readonly Lazy<bool> AllocCountWorks = new(Bench.AllocCountRecorderWorksOnCurrentThread);

        [SetUp]
        public void SuppressUnrelatedLogNoise() => BenchSetup.RearmPerTest();

        static void RequireBytesCounter()
        {
            if (!BytesCounterWorks.Value)
            {
                Assert.Ignore(
                    $"No managed-byte counter advances on this Unity/runtime configuration " +
                    $"(Bench.ByteSource = {Bench.ByteSource}). Byte assertions are not trustworthy here; " +
                    "rely on AllocCount tests below or run the suite under PlayMode / IL2CPP for byte proof.");
            }
        }

        static void RequireAllocCountRecorder()
        {
            if (!AllocCountWorks.Value)
            {
                Assert.Ignore(
                    "Profiler GC.Alloc Recorder did not advance on this thread/runtime. " +
                    "AllocCount samples cannot be trusted here.");
            }
        }

        [Test]
        public void Harness_ReportsByteCounterChoice()
        {
            TestContext.Out.WriteLine($"Bench.ByteSource = {Bench.ByteSource}");
            TestContext.Out.WriteLine($"Bench.BytesCounterWorks = {Bench.BytesCounterWorks}");
            TestContext.Out.WriteLine($"GC.Alloc Recorder works = {AllocCountWorks.Value}");
            Assert.Pass();
        }

        [Test]
        public void SanityProbe_FreshHeapObject_AllocationCountIsObserved()
        {
            RequireAllocCountRecorder();
            long count = Bench.AllocationsSingleInvocation(static () => _ = new object());
            Assert.That(count, Is.GreaterThan(0),
                "GC.Alloc Profiler marker did not fire for a fresh heap object.");
        }

        [Test]
        public void SanityProbe_LargeHeapAllocation_AllocationBytesAreObserved()
        {
            RequireBytesCounter();
            // Allocate enough to cross even a coarse heap-segment counter (TotalMemoryDelta source
            // does not resolve single-object allocations — only segment growth).
            long delta = Bench.AllocatedBytesSingleInvocation(static () =>
            {
                object? anchor = null;
                for (int i = 0; i < 256; i++)
                {
                    anchor = new byte[4096];
                }

                GC.KeepAlive(anchor);
            });
            Assert.That(delta, Is.GreaterThan(0),
                $"Byte counter ({Bench.ByteSource}) did not advance for ~1 MB of allocations.");
        }

        [Test]
        public void Map_Build_FromEmpty_Count10_SingleFreshBuild_Allocates()
        {
            RequireAllocCountRecorder();
            long count = Bench.AllocationsSingleInvocation(static () => _ = MapVsTupleDictBench.BuildMap(10));
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void Dict_Build_FromEmpty_Count10_SingleFreshBuild_Allocates()
        {
            RequireAllocCountRecorder();
            long count = Bench.AllocationsSingleInvocation(static () => _ = MapVsTupleDictBench.BuildDict(10));
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void Map_TryGetValue_Hit_Warmed_NoAlloc()
        {
            RequireAllocCountRecorder();
            Map<int, int, string> map = MapVsTupleDictBench.BuildMap(1000);
            for (int i = 0; i < 10_000; i++)
            {
                map.TryGetValue(500, 500, out string _);
            }

            Bench.NoAllocations(() => map.TryGetValue(500, 500, out string _));
        }

        [Test]
        public void Dict_TryGetValue_Hit_Warmed_NoAlloc()
        {
            RequireAllocCountRecorder();
            Dictionary<(int, int), string> dict = MapVsTupleDictBench.BuildDict(1000);
            for (int i = 0; i < 10_000; i++)
            {
                dict.TryGetValue((500, 500), out string _);
            }

            Bench.NoAllocations(() => dict.TryGetValue((500, 500), out string _));
        }

        [Test]
        public void Map_Foreach_Count1000_SinglePass_Allocates()
        {
            RequireAllocCountRecorder();
            Map<int, int, string> map = MapVsTupleDictBench.BuildMap(1000);
            long count = Bench.AllocationsSingleInvocation(() =>
            {
                int sum = 0;
                foreach (KeyValuePair<Index<int, int>, string> kv in map)
                {
                    sum += kv.Key.Primary;
                }

                VolatileSink.Use(sum);
            });

            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void Indexer_Values_Count_PerRead_NoAlloc()
        {
            RequireAllocCountRecorder();
            Map<int, int, string> map = MapIndexerValuesBench.BuildAllMatch(10);
            Indexer<int, int, string> indexer = MapIndexerValuesBench.GetIndexer(map);

            Bench.NoAllocations(() => _ = indexer.Values.Count);
        }

        [Test]
        public void GetPrimaryKeys_Count_SingleCall_Allocates()
        {
            RequireAllocCountRecorder();
            Map<int, int, string> map = MapKeyEnumerationBench.BuildMap(1000);

            long count = Bench.AllocationsSingleInvocation(() => _ = map.GetPrimaryKeys().Count());

            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void GetSecondaryKeys_Count_SingleCall_Allocates()
        {
            RequireAllocCountRecorder();
            Map<int, int, string> map = MapKeyEnumerationBench.BuildMap(1000);

            long count = Bench.AllocationsSingleInvocation(() => _ = map.GetSecondaryKeys().Count());

            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void BulkAdd_AfterFiveIndexers_Count10_SingleFreshMap_Allocates()
        {
            RequireAllocCountRecorder();
            long count = Bench.AllocationsSingleInvocation(() =>
            {
                Map<int, int, string> map = MapIndexerBulkRebuildBench.BuildMapWithFiveIndexers();
                MapIndexerBulkRebuildBench.BulkAdd(map, 10);
            });

            Assert.That(count, Is.GreaterThan(0));
        }
    }
}
