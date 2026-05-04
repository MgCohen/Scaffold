#nullable enable

using Scaffold.States;

namespace Scaffold.States.Samples
{
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
