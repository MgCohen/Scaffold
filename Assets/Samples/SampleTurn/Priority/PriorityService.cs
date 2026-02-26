using System.Collections.Generic;
using Scaffold.States;
using Sample.Turn.Mutators;
using System.Linq;

namespace Sample.Turn
{
    /// <summary>
    /// Manages who has priority to act. Reads TurnOrderState and PriorityState; syncs active players from turn owners via SetNextActivePlayers.
    /// Holds only Store.
    /// </summary>
    public class PriorityService : IPriorityService
    {
        private readonly Store _store;

        public PriorityService(Store store)
        {
            _store = store;
        }

        public void SetNextActivePlayers()
        {
            var turnOrderState = _store.Get<TurnOrderState>();
            var turnOwners = turnOrderState.TurnOwners.ToList();
            var priorityState = _store.Get<PriorityState>();
            var activePlayers = priorityState.ActivePlayers.ToList();
            SetNextActivePlayers(turnOwners, activePlayers);
        }

        private void SetNextActivePlayers(List<MatchPlayer> turnOwners, List<MatchPlayer> activePlayers)
        {
            MatchPlayer nextPlayer;
            if (activePlayers == null || activePlayers.Count <= 0)
            {
                nextPlayer = turnOwners[0];
            }
            else
            {
                var index = turnOwners.IndexOf(activePlayers[0]);
                nextPlayer = turnOwners[index];
            }
            SetActivePlayers(nextPlayer);
        }

        private void SetActivePlayers(params MatchPlayer[] players)
        {
            _store.Execute(new SetActivePlayersMutator(players));
        }

        public bool IsActive(MatchPlayer player)
        {
            var priorityState = _store.Get<PriorityState>();
            var activePlayers = priorityState.ActivePlayers.ToList();
            return activePlayers != null && activePlayers.Contains(player);
        }
    }
}
