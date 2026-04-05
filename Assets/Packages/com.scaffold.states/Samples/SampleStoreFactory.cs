#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
    /// <summary>
    /// Builds stores for the sample: canonical slices, payload registry with multiple mutators per payload,
    /// aggregate slice, and optional keyed counters for <see cref="IPayloadReference"/>.
    /// </summary>
    public static class SampleStoreFactory
    {
        public static StoreFeaturesDemo CreateFullDemo()
        {
            var totalsProvider = new TotalsAggregateProvider();
            var builder = new StoreBuilder();
            builder.AddState(new CounterState(0));
            builder.AddState(new NotesState(string.Empty));
            builder.RegisterAggregate(totalsProvider);
            builder.RegisterMutator(new ApplyCombinedTickToCounter());
            builder.RegisterMutator(new ApplyCombinedTickToNotes());
            Store store = builder.Build();
            return new StoreFeaturesDemo(store, totalsProvider);
        }

        /// <summary>
        /// Two <see cref="CounterState"/> rows keyed by <see cref="SampleKey"/>, one mutator type, payload supplies the key.
        /// </summary>
        public static Store CreateKeyedCounterDemo()
        {
            var keyA = new SampleKey("A");
            var keyB = new SampleKey("B");
            var builder = new StoreBuilder();
            builder.AddState(keyA, new CounterState(0));
            builder.AddState(keyB, new CounterState(0));
            builder.RegisterMutator(new AddDeltaToKeyedCounter());
            return builder.Build();
        }
    }

    public sealed record StoreFeaturesDemo(Store Store, TotalsAggregateProvider TotalsProvider);
}
