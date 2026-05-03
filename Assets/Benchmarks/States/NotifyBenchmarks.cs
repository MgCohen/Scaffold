using NUnit.Framework;
using Scaffold.Benchmarks;
using Scaffold.States;
using Unity.PerformanceTesting;

namespace Scaffold.Benchmarks.States
{
    public sealed class NotifyBenchmarks
    {
        [SetUp]
        public void SetUp()
        {
            BenchSetup.RearmPerTest();
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Notify_50Subs_NoMutation()
        {
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            Store store = builder.Build();

            int sum = 0;
            for (int i = 0; i < 50; i++)
            {
                store.Subscribe<CounterState>((_, s, _) => sum += s.Value);
            }

            CounterState payload = new(1);
            Bench.Measure(() => store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated),
                iterationsPer: 1_000);

            VolatileSink.Use(sum);
        }

        [Test, Performance, Category("PerformanceBenchmark")]
        public void Notify_50Subs_HalfUnsubscribeInline()
        {
            var scenario = new HalfUnsubscribeInlineScenario();
            CounterState payload = new(1);
            Bench.Measure(() => scenario.Store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated),
                iterationsPer: 1_000);

            VolatileSink.Use(scenario.Sum);
        }

        [Test]
        public void Notify_50Subs_HalfUnsubscribeInline_NoAllocations()
        {
            var scenario = new HalfUnsubscribeInlineScenario();
            CounterState payload = new(1);
            Bench.NoAllocations(() => scenario.Store.Events.Notify(Reference.Null, payload, StateChangeEvent.Updated));

            VolatileSink.Use(scenario.Sum);
        }

        private sealed class HalfUnsubscribeInlineScenario
        {
            public HalfUnsubscribeInlineScenario()
            {
                var builder = new StoreBuilder();
                builder.AddState(new CounterState(0));
                Store = builder.Build();
                Handlers = new System.Action<Reference, CounterState, StateChangeEvent>[50];
                WireHandlers();
            }

            public Store Store { get; }

            public System.Action<Reference, CounterState, StateChangeEvent>[] Handlers { get; }

            public int Sum { get; private set; }

            private void WireHandlers()
            {
                for (int i = 0; i < 50; i++)
                {
                    int idx = i;
                    Handlers[i] = (_, s, _) => { Sum += s.Value; if (idx % 2 == 0) Store.Unsubscribe(Reference.Null, Handlers[idx]); };

                    Store.Subscribe(Reference.Null, Handlers[i]);
                }
            }
        }
    }
}
