#nullable enable

using System;
using Scaffold.States;

namespace Scaffold.States.Samples
{
    /// <summary>
    /// Derived aggregate over canonical <see cref="CounterState"/> and <see cref="NotesState"/> at <see cref="Reference.Null"/>.
    /// </summary>
    public sealed class TotalsAggregateProvider : AggregateProvider<TotalsDashboardState>
    {
        /// <summary>
        /// How many times canonical <see cref="CounterState"/> / <see cref="NotesState"/> subscriptions fired during wiring.
        /// Useful for demos and tests; each fires before the aggregate rebuild callback.
        /// </summary>
        public int CanonicalSubscriptionNotifications { get; private set; }

        public override void Wire(IStoreScope scope, IAggregateRebuild rebuild)
        {
            scope.Events.Subscribe<CounterState>(Reference.Null, (_, _, _) =>
            {
                CanonicalSubscriptionNotifications++;
                rebuild.RequestRebuild();
            });
            scope.Events.Subscribe<NotesState>(Reference.Null, (_, _, _) =>
            {
                CanonicalSubscriptionNotifications++;
                rebuild.RequestRebuild();
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
