using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.Maps
{
    /// <summary>
    /// Measures key enumeration allocations (<see cref="Map{TPrimary,TSecondary,TValue}.GetPrimaryKeys"/> / GetSecondaryKeys).
    /// Phase 1 returns the <see cref="HashSet{T}"/> directly (no per-call <c>List&lt;T&gt;</c> copy).
    /// </summary>
    public sealed class MapKeyEnumerationBench
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        internal static Map<int, int, string> BuildMap(int entries)
        {
            Map<int, int, string> map = new Map<int, int, string>();
            for (int i = 0; i < entries; i++)
            {
                map.Add(i, i + entries, "v" + i);
            }

            return map;
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void GetPrimaryKeys_1000Entries_10000Calls()
        {
            Map<int, int, string> map = BuildMap(1000);

            Bench.Measure(() =>
            {
                _ = map.GetPrimaryKeys().Count();
            }, iterationsPer: 10_000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void GetSecondaryKeys_1000Entries_10000Calls()
        {
            Map<int, int, string> map = BuildMap(1000);

            Bench.Measure(() =>
            {
                _ = map.GetSecondaryKeys().Count();
            }, iterationsPer: 10_000);
        }
    }
}
