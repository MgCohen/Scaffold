using NUnit.Framework;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for the equality contract used by <c>RegisteredMutator.Apply</c>
    /// (audit §4.13). Today the dispatcher calls <c>executeReference.Equals(Reference.Null)</c>;
    /// Phase 1 swaps to <c>ReferenceEquals(executeReference, Reference.Null)</c>. Measures both
    /// against a deliberately-misbehaving <see cref="object.Equals(object?)"/> override on a
    /// helper type to expose the cliff in the audit's "≥10× faster" success criterion.
    /// </summary>
    public sealed class ReferenceEqualityBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        sealed class SlowEqualsProbe
        {
            public override bool Equals(object? obj)
            {
                // Deliberately quadratic-ish work in Equals to expose the cliff.
                int sum = 0;
                for (int i = 0; i < 64; i++)
                {
                    sum += i;
                }

                VolatileSink.Use(sum);
                return ReferenceEquals(this, obj);
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Equals_VirtualDispatch_VsReferenceNull()
        {
            SlowEqualsProbe probe = new SlowEqualsProbe();
            Reference rNull = Reference.Null;
            Bench.Measure(() => _ = probe.Equals(rNull), iterationsPer: 1_000_000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void ReferenceEquals_VsReferenceNull()
        {
            SlowEqualsProbe probe = new SlowEqualsProbe();
            Reference rNull = Reference.Null;
            Bench.Measure(() => _ = ReferenceEquals(probe, rNull), iterationsPer: 1_000_000);
        }
    }
}
