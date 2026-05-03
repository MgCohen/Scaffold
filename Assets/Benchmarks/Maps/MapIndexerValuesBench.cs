using NUnit.Framework;
using Scaffold.Maps;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.Maps
{
    /// <summary>
    /// Measures per-call work for <see cref="Indexer{TPrimary,TSecondary,TValue}.Values"/> <c>.Count</c> (Phase 2+: O(1), no list snapshot).
    /// </summary>
    public sealed class MapIndexerValuesBench
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        internal static Map<int, int, string> BuildAllMatch(int entryCount)
        {
            Map<int, int, string> map = new Map<int, int, string>();
            for (int i = 0; i < entryCount; i++)
            {
                map.Add(i, i, "x" + i);
            }

            map.AddIndexer("all", (_, _) => true);
            return map;
        }

        [Test, Performance]
        public void Indexer_Values_PerRead_Count10()
        {
            Map<int, int, string> map = BuildAllMatch(10);
            Indexer<int, int, string> indexer = GetIndexer(map);

            Bench.Measure(() =>
            {
                _ = indexer.Values.Count;
            }, iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Indexer_Values_PerRead_Count100()
        {
            Map<int, int, string> map = BuildAllMatch(100);
            Indexer<int, int, string> indexer = GetIndexer(map);

            Bench.Measure(() =>
            {
                _ = indexer.Values.Count;
            }, iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Indexer_Values_PerRead_Count1000()
        {
            Map<int, int, string> map = BuildAllMatch(1000);
            Indexer<int, int, string> indexer = GetIndexer(map);

            Bench.Measure(() =>
            {
                _ = indexer.Values.Count;
            }, iterationsPer: 10_000);
        }

        internal static Indexer<int, int, string> GetIndexer(Map<int, int, string> map)
        {
            Assert.That(map.TryGetIndexer("all", out IReadOnlyIndexer<int, int, string> ro), Is.True);
            return (Indexer<int, int, string>)ro;
        }
    }
}
