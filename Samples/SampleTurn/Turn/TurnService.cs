using System;
using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Manages the lifecycle of Turn entities. Creates a new Turn each time a turn starts
    /// and delegates phase execution to it. Holds no mutable state itself.
    /// </summary>
    public class TurnService : ITurnService
    {
        private readonly IReadOnlyList<Phase> _phases;
        private readonly Store _store;
        private readonly Action _onTurnEnded;

        public TurnService(IReadOnlyList<Phase> phases, Store store, Action onTurnEnded = null)
        {
            _phases = phases;
            _store = store;
            _onTurnEnded = onTurnEnded;
        }

        public void StartTurn()
        {
            if (_phases == null || _phases.Count == 0) return;
            var turn = new Turn(_phases, _store, onTurnEnded: _onTurnEnded);
            turn.RunCurrentPhase();
        }
    }
}
