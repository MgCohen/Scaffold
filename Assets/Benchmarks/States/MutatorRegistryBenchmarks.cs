using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    /// <summary>
    /// Phase 0 baseline for <c>MutatorRegistry.TryGet</c>. The registry is internal; this benchmark
    /// has access via <c>InternalsVisibleTo("Scaffold.Benchmarks.States")</c> on
    /// <c>Scaffold.States/Runtime/AssemblyInfo.cs</c>. Serves as the floor for the Phase 5
    /// source-generated dispatcher.
    /// </summary>
    public sealed class MutatorRegistryBenchmarks
    {
        [SetUp]
        public void SetUp() => BenchSetup.RearmPerTest();

        // Distinct payload types so the registry's Dictionary<Type, …> has 50 entries to choose from.
        // PayloadCarrier<T> is a generic carrier that yields a fresh closed Type per type argument.
        sealed record PayloadCarrier<T>(int N);

        sealed class CarrierMutator<T> : Mutator<CounterState, PayloadCarrier<T>>
        {
            public override CounterState Change(CounterState state, PayloadCarrier<T> payload, IStateScope scope)
                => new(state.Value + payload.N);
        }

        static MutatorRegistry BuildRegistryWith50Types()
        {
            var registry = new MutatorRegistry();
            // Hand-rolled 50 distinct closed generic payload types.
            registry.Register(new CarrierMutator<int>());
            registry.Register(new CarrierMutator<long>());
            registry.Register(new CarrierMutator<short>());
            registry.Register(new CarrierMutator<byte>());
            registry.Register(new CarrierMutator<sbyte>());
            registry.Register(new CarrierMutator<uint>());
            registry.Register(new CarrierMutator<ulong>());
            registry.Register(new CarrierMutator<ushort>());
            registry.Register(new CarrierMutator<float>());
            registry.Register(new CarrierMutator<double>());
            registry.Register(new CarrierMutator<decimal>());
            registry.Register(new CarrierMutator<char>());
            registry.Register(new CarrierMutator<bool>());
            registry.Register(new CarrierMutator<DateTime>());
            registry.Register(new CarrierMutator<TimeSpan>());
            registry.Register(new CarrierMutator<Guid>());
            registry.Register(new CarrierMutator<string>());
            registry.Register(new CarrierMutator<object>());
            registry.Register(new CarrierMutator<Uri>());
            registry.Register(new CarrierMutator<Version>());
            registry.Register(new CarrierMutator<int[]>());
            registry.Register(new CarrierMutator<long[]>());
            registry.Register(new CarrierMutator<string[]>());
            registry.Register(new CarrierMutator<List<int>>());
            registry.Register(new CarrierMutator<List<long>>());
            registry.Register(new CarrierMutator<List<string>>());
            registry.Register(new CarrierMutator<HashSet<int>>());
            registry.Register(new CarrierMutator<HashSet<long>>());
            registry.Register(new CarrierMutator<HashSet<string>>());
            registry.Register(new CarrierMutator<Dictionary<int, int>>());
            registry.Register(new CarrierMutator<Dictionary<int, string>>());
            registry.Register(new CarrierMutator<Dictionary<string, string>>());
            registry.Register(new CarrierMutator<KeyValuePair<int, int>>());
            registry.Register(new CarrierMutator<KeyValuePair<string, int>>());
            registry.Register(new CarrierMutator<Tuple<int, int>>());
            registry.Register(new CarrierMutator<Tuple<int, string>>());
            registry.Register(new CarrierMutator<ValueTuple<int, int>>());
            registry.Register(new CarrierMutator<ValueTuple<int, string>>());
            registry.Register(new CarrierMutator<ValueTuple<string, string>>());
            registry.Register(new CarrierMutator<Action>());
            registry.Register(new CarrierMutator<Func<int>>());
            registry.Register(new CarrierMutator<Func<int, int>>());
            registry.Register(new CarrierMutator<Predicate<int>>());
            registry.Register(new CarrierMutator<Comparison<int>>());
            registry.Register(new CarrierMutator<EventArgs>());
            registry.Register(new CarrierMutator<Exception>());
            registry.Register(new CarrierMutator<ArgumentException>());
            registry.Register(new CarrierMutator<InvalidOperationException>());
            registry.Register(new CarrierMutator<NotSupportedException>());
            registry.Register(new CarrierMutator<Random>());
            return registry;
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void TryGet_KnownKey_50TypesRegistered()
        {
            MutatorRegistry registry = BuildRegistryWith50Types();
            Type knownKey = typeof(PayloadCarrier<int>);

            Bench.Measure(() => _ = registry.TryGet(knownKey, out _),
                iterationsPer: 100_000);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void TryGet_UnknownKey_50TypesRegistered()
        {
            MutatorRegistry registry = BuildRegistryWith50Types();
            Type unknownKey = typeof(PayloadCarrier<DayOfWeek>);

            Bench.Measure(() => _ = registry.TryGet(unknownKey, out _),
                iterationsPer: 100_000);
        }
    }
}
