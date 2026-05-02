#nullable enable
using System;
using System.Runtime.CompilerServices;
using Unity.PerformanceTesting;
using UnityEngine.Profiling;

namespace Scaffold.Maps.Tests.Performance
{
    /// <summary>
    /// Shared harness per Docs/Audits/Packages/_benchmarking.md.
    /// </summary>
    public static class Bench
    {
        /// <summary>
        /// Source the harness will use to read managed bytes allocated.
        /// Picked once at first use to match the actual runtime behaviour.
        /// </summary>
        public enum AllocByteSource
        {
            /// <summary><see cref="GC.GetAllocatedBytesForCurrentThread"/> — per-thread cumulative. Some Unity Editor / Mono configs report 0.</summary>
            CurrentThreadAllocatedBytes,
            /// <summary><see cref="GC.GetTotalMemory"/> delta — heap size, only accurate when no Gen0 fires inside the measurement window.</summary>
            TotalMemoryDelta,
            /// <summary>None of the byte counters advance on this runtime — bytes will always read 0. Use the alloc-count signal instead.</summary>
            None,
        }

        static readonly Func<long> BytesReader;
        public static AllocByteSource ByteSource { get; }

        static Bench()
        {
            (BytesReader, ByteSource) = SelectBytesReader();
        }

        static (Func<long>, AllocByteSource) SelectBytesReader()
        {
            // Per-thread cumulative counter. Works on most .NET runtimes; reports 0 on some Unity Mono configs.
            try
            {
                if (Probe(static () => GC.GetAllocatedBytesForCurrentThread()))
                {
                    return (static () => GC.GetAllocatedBytesForCurrentThread(), AllocByteSource.CurrentThreadAllocatedBytes);
                }
            }
            catch
            {
                // PlatformNotSupportedException on exotic runtimes — try the next option.
            }

            // Last resort: heap-size delta. Only meaningful when no Gen0 fires inside the window.
            try
            {
                if (Probe(static () => GC.GetTotalMemory(false)))
                {
                    return (static () => GC.GetTotalMemory(false), AllocByteSource.TotalMemoryDelta);
                }
            }
            catch
            {
            }

            return (static () => 0L, AllocByteSource.None);
        }

        static bool Probe(Func<long> reader)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = reader();
            object? anchor = null;
            for (int i = 0; i < 1024; i++)
            {
                anchor = new byte[256];
            }

            _ = anchor;
            return reader() > before;
        }

        /// <summary>
        /// Per-op time and allocation profile. Reports six sample groups:
        /// Time (ns/op), Allocated (bytes/op), AllocCount (GC.Alloc events/op),
        /// Gen0/Gen1/Gen2 (collections per measurement window).
        /// Sample group names include <paramref name="measurementId"/> so Unity does not merge results across tests.
        /// </summary>
        /// <param name="measurementId">
        /// Defaults to the calling member name. If two benchmarks could share a name, pass an explicit id
        /// (for example <c>nameof(YourType) + "." + nameof(YourTest)</c>).
        /// </param>
        public static void Measure(
            Action action,
            int warmup = 10,
            int measurements = 20,
            int iterationsPer = 1000,
            [CallerMemberName] string measurementId = "")
        {
            string prefix = string.IsNullOrEmpty(measurementId) ? "Bench" : measurementId;
            SampleGroup time = new($"{prefix}:Time", SampleUnit.Nanosecond);
            SampleGroup bytes = new($"{prefix}:Allocated", SampleUnit.Byte);
            SampleGroup allocs = new($"{prefix}:AllocCount", SampleUnit.Undefined);
            SampleGroup gen0 = new($"{prefix}:Gen0", SampleUnit.Undefined);
            SampleGroup gen1 = new($"{prefix}:Gen1", SampleUnit.Undefined);
            SampleGroup gen2 = new($"{prefix}:Gen2", SampleUnit.Undefined);

            Recorder gcAllocRecorder = Recorder.Get("GC.Alloc");
            gcAllocRecorder.enabled = false;
            gcAllocRecorder.FilterToCurrentThread();

            for (int i = 0; i < warmup; i++)
            {
                action();
            }

            for (int m = 0; m < measurements; m++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long b0 = BytesReader();
                int g00 = GC.CollectionCount(0);
                int g10 = GC.CollectionCount(1);
                int g20 = GC.CollectionCount(2);

                gcAllocRecorder.enabled = true;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < iterationsPer; i++)
                {
                    action();
                }

                sw.Stop();
                gcAllocRecorder.enabled = false;

                long bytesDelta = BytesReader() - b0;
                if (bytesDelta < 0)
                {
                    // Heap-size source dipped below baseline because a collection ran inside the window.
                    bytesDelta = 0;
                }

                long bytesPerOp = bytesDelta / iterationsPer;
                long allocsTotal = gcAllocRecorder.sampleBlockCount;
                double nsPerOp = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterationsPer;
                double allocsPerOp = (double)allocsTotal / iterationsPer;

                Unity.PerformanceTesting.Measure.Custom(time, nsPerOp);
                Unity.PerformanceTesting.Measure.Custom(bytes, bytesPerOp);
                Unity.PerformanceTesting.Measure.Custom(allocs, allocsPerOp);
                Unity.PerformanceTesting.Measure.Custom(gen0, GC.CollectionCount(0) - g00);
                Unity.PerformanceTesting.Measure.Custom(gen1, GC.CollectionCount(1) - g10);
                Unity.PerformanceTesting.Measure.Custom(gen2, GC.CollectionCount(2) - g20);
            }
        }

        /// <summary>
        /// Whether <see cref="ByteSource"/> resolved to a counter that actually advances on this runtime.
        /// When false, the <c>:Allocated</c> sample group will read 0 — rely on <c>:AllocCount</c> (from
        /// the <c>GC.Alloc</c> Profiler marker) and Gen0/1/2 instead, or run the suite under PlayMode/IL2CPP.
        /// </summary>
        public static bool BytesCounterWorks => ByteSource != AllocByteSource.None;

        /// <summary>
        /// Whether the <c>GC.Alloc</c> Profiler marker advances on the current managed thread.
        /// This is independent of <see cref="BytesCounterWorks"/> and is typically true in EditMode.
        /// </summary>
        public static bool AllocCountRecorderWorksOnCurrentThread()
        {
            Recorder rec = Recorder.Get("GC.Alloc");
            rec.FilterToCurrentThread();
            rec.enabled = false;
            // Drain whatever the harness/JIT already captured.
            _ = rec.sampleBlockCount;

            rec.enabled = true;
            object? anchor = null;
            for (int i = 0; i < 1024; i++)
            {
                anchor = new byte[256];
            }

            _ = anchor;
            rec.enabled = false;
            return rec.sampleBlockCount > 0;
        }

        /// <summary>
        /// Bytes allocated between entry and exit of one <paramref name="action"/> invocation,
        /// after JIT/static warmup and a full GC. Reads from <see cref="ByteSource"/> — returns 0
        /// when no byte counter advances on this runtime.
        /// </summary>
        public static long AllocatedBytesSingleInvocation(Action action)
        {
            action();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = BytesReader();
            action();
            long delta = BytesReader() - before;
            return delta < 0 ? 0 : delta;
        }

        /// <summary>
        /// Number of <c>GC.Alloc</c> Profiler-marker events recorded on the current thread for one
        /// <paramref name="action"/> invocation, after JIT/static warmup. Independent of bytes — useful when
        /// the byte counter is broken on the current runtime.
        /// </summary>
        public static long AllocationsSingleInvocation(Action action)
        {
            action();
            Recorder rec = Recorder.Get("GC.Alloc");
            rec.FilterToCurrentThread();
            rec.enabled = false;
            _ = rec.sampleBlockCount;

            rec.enabled = true;
            action();
            rec.enabled = false;
            return rec.sampleBlockCount;
        }

        /// <summary>
        /// Asserts a piece of code allocates zero bytes / zero allocation events. Use inside [Test] (no [Performance] needed).
        /// Falls back to the alloc-count signal when no byte counter advances on this runtime.
        /// </summary>
        public static void NoAllocations(Action action)
        {
            action(); // warm JIT, prime statics

            if (BytesCounterWorks)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long before = BytesReader();
                action();
                long allocated = BytesReader() - before;
                if (allocated > 0)
                {
                    throw new InvalidOperationException($"Allocated {allocated} bytes; expected 0.");
                }

                return;
            }

            long count = AllocationsSingleInvocation(action);
            if (count > 0)
            {
                throw new InvalidOperationException(
                    $"Recorded {count} GC.Alloc event(s); expected 0. (Byte counter unavailable on this runtime; using GC.Alloc marker count.)");
            }
        }
    }
}
