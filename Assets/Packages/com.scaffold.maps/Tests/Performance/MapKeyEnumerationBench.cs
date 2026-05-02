using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

namespace Scaffold.Maps.Tests.Performance
{
    /// <summary>
    /// Measures key enumeration allocations (<see cref="Map{TPrimary,TSecondary,TValue}.GetPrimaryKeys"/> / GetSecondaryKeys).
    /// </summary>
    public sealed class MapKeyEnumerationBench
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        internal static Map<int, int, string> BuildMap(int entries)
        {
            Map<int, int, string> map = new Map<int, int, string>();
            for (int i = 0; i < entries; i++)
            {
                map.Add(i, i + entries, "v" + i);
            }

            return map;
        }

        [Test, Performance]
        public void GetPrimaryKeys_1000Entries_10000Calls()
        {
            Map<int, int, string> map = BuildMap(1000);

            Bench.Measure(() =>
            {
                _ = map.GetPrimaryKeys().Count();
            }, iterationsPer: 10_000);
        }

        [Test, Performance]
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
