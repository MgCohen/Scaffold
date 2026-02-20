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
        private readonly TurnOrderService _turnOrderService;
        private readonly IPriorityService _priorityService;
        private readonly ITurnService _turnService;

        public Match(IReadOnlyList<MatchPlayer> players, IReadOnlyList<Phase> phases, Store store)
        {
            _store = store;
            _turnOrderService = new TurnOrderService(store);
            _priorityService = new PriorityService(store);
            _turnService = new TurnService(phases, store, onTurnEnded: OnTurnEnded);
        }

        public void StartTurn()
        {
            _turnService.StartTurn();
        }

        public void EndRound()
        {
            _turnOrderService.MoveToFirst();
            _priorityService.SetNextActivePlayers();
            _store.Execute(new EndRoundMutator());
        }

        private void OnTurnEnded()
        {
            _turnOrderService.AdvanceToNext();
            _priorityService.SetNextActivePlayers();
            _turnService.StartTurn();
        }
    }
}
