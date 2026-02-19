using System.Collections.Generic;
using Scaffold.States;
using Sample.Turn.Mutators;

namespace Sample.Turn
{
    /// <summary>
    /// Orchestrates a match: holds player and phase entities, creates services, and wires them together.
    /// All state lives in the Store; all behaviour lives in the services.
    /// </summary>
    public class Match
    {
        private readonly Store _store;
        private readonly PlayerPriorityService _priorityService;
        private readonly TurnService _turnService;

        public Match(IReadOnlyList<MatchPlayer> players, IReadOnlyList<Phase> phases, Store store)
        {
            _store = store;
            _priorityService = new PlayerPriorityService(store);
            _turnService = new TurnService(phases, store, onTurnEnded: OnTurnEnded);
        }

        public void StartTurn()
        {
            _turnService.StartTurn();
        }

        public void EndRound()
        {
            _priorityService.ResetToFirstPlayer();
            _store.Execute(new EndRoundMutator());
        }

        private void OnTurnEnded()
        {
            _priorityService.AdvanceTurn();
            _turnService.StartTurn();
        }
    }
}
