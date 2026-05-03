#nullable enable

using System;
using Scaffold.States;

namespace Scaffold.States.Samples
{
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
}
