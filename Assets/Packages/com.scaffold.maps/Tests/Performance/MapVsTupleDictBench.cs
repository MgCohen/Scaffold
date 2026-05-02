using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

namespace Scaffold.Maps.Tests.Performance
{
    /// <summary>
    /// Sanity baseline: Map&lt;int,int,string&gt; vs Dictionary&lt;(int,int),string&gt;.
    /// </summary>
    public sealed class MapVsTupleDictBench
    {
        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        internal static Map<int, int, string> BuildMap(int n)
        {
            Map<int, int, string> map = new Map<int, int, string>();
            for (int i = 0; i < n; i++)
            {
                map.Add(i, i, "v" + i);
            }

            return map;
        }

        internal static Dictionary<(int, int), string> BuildDict(int n)
        {
            Dictionary<(int, int), string> dict = new Dictionary<(int, int), string>();
            for (int i = 0; i < n; i++)
            {
                dict[(i, i)] = "v" + i;
            }

            return dict;
        }

        [Test, Performance]
        public void Map_Add_FromEmpty_Count10()
        {
            Bench.Measure(() =>
            {
                _ = BuildMap(10);
            }, iterationsPer: 500);
        }

        [Test, Performance]
        public void Dict_Add_FromEmpty_Count10()
        {
            Bench.Measure(() =>
            {
                _ = BuildDict(10);
            }, iterationsPer: 500);
        }

        [Test, Performance]
        public void Map_Add_FromEmpty_Count100()
        {
            Bench.Measure(() =>
            {
                _ = BuildMap(100);
            }, iterationsPer: 100);
        }

        [Test, Performance]
        public void Dict_Add_FromEmpty_Count100()
        {
            Bench.Measure(() =>
            {
                _ = BuildDict(100);
            }, iterationsPer: 100);
        }

        [Test, Performance]
        public void Map_Add_FromEmpty_Count1000()
        {
            Bench.Measure(() =>
            {
                _ = BuildMap(1000);
            }, iterationsPer: 20);
        }

        [Test, Performance]
        public void Dict_Add_FromEmpty_Count1000()
        {
            Bench.Measure(() =>
            {
                _ = BuildDict(1000);
            }, iterationsPer: 20);
        }

        [Test, Performance]
        public void Map_TryGetValue_Hit_Count1000()
        {
            Map<int, int, string> map = BuildMap(1000);

            Bench.Measure(() =>
            {
                map.TryGetValue(500, 500, out string _);
            }, iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Dict_TryGetValue_Hit_Count1000()
        {
            Dictionary<(int, int), string> dict = BuildDict(1000);

            Bench.Measure(() =>
            {
                dict.TryGetValue((500, 500), out string _);
            }, iterationsPer: 10_000);
        }

        [Test, Performance]
        public void Map_Foreach_Count1000()
        {
            Map<int, int, string> map = BuildMap(1000);

            Bench.Measure(() =>
            {
                int sum = 0;
                foreach (KeyValuePair<Index<int, int>, string> kv in map)
                {
                    sum += kv.Key.Primary;
                }

                VolatileSink.Use(sum);
            }, iterationsPer: 500);
        }

        [Test, Performance]
        public void Dict_Foreach_Count1000()
        {
            Dictionary<(int, int), string> dict = BuildDict(1000);

            Bench.Measure(() =>
            {
                int sum = 0;
                foreach (KeyValuePair<(int, int), string> kv in dict)
                {
                    sum += kv.Key.Item1;
                }

                VolatileSink.Use(sum);
            }, iterationsPer: 500);
        }
    }

    internal static class VolatileSink
    {
        internal static volatile int Value;

        internal static void Use(int x)
        {
            Value = x;
        }
    }
}
