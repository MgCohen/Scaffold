#nullable enable

using System;
using Scaffold.States;

namespace Scaffold.States.Tests.Fixtures
{
    // Mirror of UPM sample types under Samples~/ (tests cannot reference the non-imported samples assembly — ExecPlan Phase 6).
    public sealed record SampleKey(string Name) : Reference;

    public sealed record CounterState(int Value) : State;

    public sealed record NotesState(string Text) : State;

    public sealed record TotalsDashboardState(int CounterValue, int NoteCharacterCount) : AggregateState;

    public sealed record CombinedTickPayload(int Delta);

    public sealed record RoutedCounterPayload(SampleKey Target, int Delta) : IPayloadReference
    {
        public Reference GetReference() => Target;
    }

    public sealed class IncrementCounterMutator : Mutator<CounterState>
    {
        private readonly int amount;

        public IncrementCounterMutator(int amount)
        {
            this.amount = amount;
        }

        public override CounterState Change(CounterState state, IStateScope scope)
        {
            return new CounterState(state.Value + amount);
        }
    }

    public sealed class ApplyCombinedTickToCounter : Mutator<CounterState, CombinedTickPayload>
    {
        public override CounterState Change(CounterState state, CombinedTickPayload payload, IStateScope scope)
        {
            return new CounterState(state.Value + payload.Delta);
        }
    }

    public sealed class ApplyCombinedTickToNotes : Mutator<NotesState, CombinedTickPayload>
    {
        public override NotesState Change(NotesState state, CombinedTickPayload payload, IStateScope scope)
        {
            return new NotesState(state.Text + new string('a', payload.Delta));
        }
    }

    public sealed class AddDeltaToKeyedCounter : Mutator<CounterState, RoutedCounterPayload>
    {
        public override CounterState Change(CounterState state, RoutedCounterPayload payload, IStateScope scope)
        {
            return new CounterState(state.Value + payload.Delta);
        }
    }

    public sealed class TotalsAggregateProvider : AggregateProvider<TotalsDashboardState>
    {
        public int CanonicalSubscriptionNotifications { get; private set; }

        public override IDisposable Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            Action<Reference, CounterState, StateChangeEvent> counterCb = (_, _, _) =>
            {
                CanonicalSubscriptionNotifications++;
                rebuild.RequestRebuild();
            };

            Action<Reference, NotesState, StateChangeEvent> notesCb = (_, _, _) =>
            {
                CanonicalSubscriptionNotifications++;
                rebuild.RequestRebuild();
            };

            scope.Events.Subscribe<CounterState>(Reference.Null, counterCb);
            scope.Events.Subscribe<NotesState>(Reference.Null, notesCb);
            return new CallbackDisposable(() =>
            {
                scope.Events.Unsubscribe<CounterState>(Reference.Null, counterCb);
                scope.Events.Unsubscribe<NotesState>(Reference.Null, notesCb);
            });
        }

        protected override TotalsDashboardState BuildCore(IStateScope scope)
        {
            CounterState counter = scope.Get<CounterState>();
            NotesState notes = scope.Get<NotesState>();
            return new TotalsDashboardState(counter.Value, notes.Text.Length);
        }
    }

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
