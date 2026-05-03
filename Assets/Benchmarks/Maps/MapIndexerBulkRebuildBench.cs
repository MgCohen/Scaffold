using NUnit.Framework;
using Scaffold.Maps;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.Maps
{
    /// <summary>
    /// Measures indexer rebuild cost when bulk-adding entries with multiple predicates (audit §11).
    /// </summary>
    public sealed class MapIndexerBulkRebuildBench
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        internal static Map<int, int, string> BuildMapWithFiveIndexers()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.AddIndexer("all", (_, _) => true);
            map.AddIndexer("sel10", (_, s) => s % 10 == 0);
            map.AddIndexer("sel50", (_, s) => s % 2 == 0);
            map.AddIndexer("sel90", (_, s) => s % 10 != 0);
            map.AddIndexer("none", (_, _) => false);
            return map;
        }

        internal static void BulkAdd(Map<int, int, string> map, int count)
        {
            for (int i = 0; i < count; i++)
            {
                map.Add(i, i, "v" + i);
            }
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void BulkAdd_AfterFiveIndexers_Count_10()
        {
            Bench.Measure(() =>
            {
                Map<int, int, string> map = BuildMapWithFiveIndexers();
                BulkAdd(map, 10);
            }, iterationsPer: 100);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void BulkAdd_AfterFiveIndexers_Count_100()
        {
            Bench.Measure(() =>
            {
                Map<int, int, string> map = BuildMapWithFiveIndexers();
                BulkAdd(map, 100);
            }, iterationsPer: 50);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void BulkAdd_AfterFiveIndexers_Count_1000()
        {
            Bench.Measure(() =>
            {
                Map<int, int, string> map = BuildMapWithFiveIndexers();
                BulkAdd(map, 1000);
            }, iterationsPer: 10);
        }
    }
}
