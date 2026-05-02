using System;
using Unity.PerformanceTesting;

namespace Scaffold.Maps.Tests.Performance
{
    /// <summary>
    /// Shared harness per Docs/Audits/Packages/_benchmarking.md.
    /// </summary>
    public static class Bench
    {
        static readonly SampleGroup Time = new("Time", SampleUnit.Nanosecond);
        static readonly SampleGroup Bytes = new("Allocated", SampleUnit.Byte);
        static readonly SampleGroup Gen0 = new("Gen0", SampleUnit.Undefined);
        static readonly SampleGroup Gen1 = new("Gen1", SampleUnit.Undefined);
        static readonly SampleGroup Gen2 = new("Gen2", SampleUnit.Undefined);

        /// <summary>
        /// Per-op time and allocation profile. Reports five sample groups:
        /// Time (ns/op), Allocated (bytes/op), Gen0/Gen1/Gen2 (collections per measurement window).
        /// </summary>
        public static void Measure(Action action,
            int warmup = 10,
            int measurements = 20,
            int iterationsPer = 1000)
        {
            for (int i = 0; i < warmup; i++)
            {
                action();
            }

            for (int m = 0; m < measurements; m++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long b0 = GC.GetAllocatedBytesForCurrentThread();
                int g00 = GC.CollectionCount(0);
                int g10 = GC.CollectionCount(1);
                int g20 = GC.CollectionCount(2);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < iterationsPer; i++)
                {
                    action();
                }

                sw.Stop();

                long bytesPerOp = (GC.GetAllocatedBytesForCurrentThread() - b0) / iterationsPer;
                double nsPerOp = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterationsPer;

                Unity.PerformanceTesting.Measure.Custom(Time, nsPerOp);
                Unity.PerformanceTesting.Measure.Custom(Bytes, bytesPerOp);
                Unity.PerformanceTesting.Measure.Custom(Gen0, GC.CollectionCount(0) - g00);
                Unity.PerformanceTesting.Measure.Custom(Gen1, GC.CollectionCount(1) - g10);
                Unity.PerformanceTesting.Measure.Custom(Gen2, GC.CollectionCount(2) - g20);
            }
        }

        /// <summary>
        /// Asserts a piece of code allocates zero bytes. Use inside [Test] (no [Performance] needed).
        /// </summary>
        public static void NoAllocations(Action action)
        {
            action(); // warm JIT, prime statics
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated != 0)
            {
                throw new InvalidOperationException($"Allocated {allocated} bytes; expected 0.");
            }
        }
    }
}
