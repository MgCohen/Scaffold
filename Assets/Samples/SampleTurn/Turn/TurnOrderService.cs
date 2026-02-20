using System.Collections.Generic;
using System.Linq;
using Scaffold.States;
using Sample.Turn.Mutators;

namespace Sample.Turn
{
    /// <summary>
    /// Manages turn order: who owns the turn and advancing to next. Holds only Store; reads TurnOrderState, mutates via mutators.
    /// </summary>
    public class TurnOrderService
    {
        private readonly Store _store;

        public TurnOrderService(Store store)
        {
            _store = store;
        }

        public void AdvanceToNext()
        {
            var state = _store.Get<TurnOrderState>();
            var nextTurnOwners = ComputeNextTurnOwners(state);
            var list = new List<MatchPlayer>(nextTurnOwners);
            _store.Execute(new SetTurnOwnersMutator(list));
        }

        public void MoveToFirst()
        {
            var state = _store.Get<TurnOrderState>();
            var first = state.PlayerOrder[0];
            var list = new List<MatchPlayer> { first };
            _store.Execute(new SetTurnOwnersMutator(list));
        }

        private IEnumerable<MatchPlayer> ComputeNextTurnOwners(TurnOrderState state)
        {
            var order = state.PlayerOrder.ToList();
            if (order.Count == 0)
            {
                return System.Array.Empty<MatchPlayer>();
            }
            var current = state.TurnOwners != null && state.TurnOwners.Count > 0 ? state.TurnOwners[0] : null;
            if (current == null)
            {
                return new List<MatchPlayer> { order[0] };
            }
            var currentIndex = order.IndexOf(current);
            var nextIndex = (currentIndex + 1) % order.Count;
            return new List<MatchPlayer> { order[nextIndex] };
        }
    }
}
