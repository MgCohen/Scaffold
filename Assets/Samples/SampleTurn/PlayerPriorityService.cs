using System.Collections.Generic;
using System.Linq;
using Scaffold.States;
using Sample.Turn.Mutators;

namespace Sample.Turn
{
    /// <summary>
    /// Manages which players are active and the turn order. Holds no state itself;
    /// reads from PlayerPriorityState in the Store and mutates via mutators.
    /// </summary>
    public class PlayerPriorityService
    {
        private readonly Store _store;

        public PlayerPriorityService(Store store)
        {
            _store = store;
        }

        public void AdvanceTurn()
        {
            var state = _store.Get<PlayerPriorityState>();
            var nextPlayer = GetNextPlayers(state);
            SetActivePlayers(nextPlayer.ToArray());
        }

        public bool IsActive(MatchPlayer player)
        {
            var activePlayers = _store.Get<PlayerPriorityState>().ActivePlayers;
            return activePlayers != null && activePlayers.Contains(player);
        }

        private IEnumerable<MatchPlayer> GetNextPlayers(PlayerPriorityState state)
        {
            var order = state.PlayerOrder.ToList();

            var currentPlayer = state.ActivePlayers != null && state.ActivePlayers.Count > 0 ? state.ActivePlayers[0] : null;
            if(currentPlayer == null)
            {
                return new List<MatchPlayer>() { order[0] };
            }
            var currentIndex = order.IndexOf(currentPlayer);
            var nextIndex = (currentIndex + 1) % order.Count;
            return new List<MatchPlayer>() { order[nextIndex] };
        }

        private void SetActivePlayers(params MatchPlayer[] players)
        {
            var playerList = new List<MatchPlayer>(players);
            _store.Execute(new SetActivePlayersMutator(playerList));
        }

        public void ResetToFirstPlayer()
        {
            var state = _store.Get<PlayerPriorityState>();
            SetActivePlayers(state.PlayerOrder[0]);
        }


        public IReadOnlyList<MatchPlayer> GetActivePlayers()
        {
            return _store.Get<PlayerPriorityState>().ActivePlayers;
        }
    }
}
