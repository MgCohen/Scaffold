using System;
using System.Collections.Generic;
using Scaffold.States;
using Sample.Turn.Mutators;

namespace Sample.Turn
{
    /// <summary>
    /// Represents a single turn (one player's run through the phase list). Create a new Turn every time a turn starts.
    /// Holds no mutable state beyond the phase index cursor. Active players are read from the Store when needed.
    /// </summary>
    public class Turn
    {
        private readonly IReadOnlyList<Phase> _phases;
        private readonly Store _store;
        private readonly Action _onTurnEnded;
        private int _currentPhaseIndex;

        public Turn(IReadOnlyList<Phase> phases, Store store, Action onTurnEnded = null)
        {
            _phases = phases ?? Array.Empty<Phase>();
            _store = store;
            _onTurnEnded = onTurnEnded;
            _currentPhaseIndex = -1;
            _store.Subscribe<TurnState>(OnTurnStateChanged);
        }

        public void AdvancePhase()
        {
            if (_phases.Count == 0) return;
            if (IsLastPhase()) { _onTurnEnded?.Invoke(); return; }
            _currentPhaseIndex++;
            RunCurrentPhase();
        }

        public void RunCurrentPhase()
        {
            if (_phases.Count == 0) return;
            if (_currentPhaseIndex < 0) _currentPhaseIndex = 0;
            _store.Execute(new SetCurrentPhaseMutator(_phases[_currentPhaseIndex]));
        }

        private bool IsLastPhase()
        {
            return _currentPhaseIndex + 1 >= _phases.Count;
        }

        private void EnterPhase(Phase phase)
        {
            var priorityState = _store.Get<PriorityState>();
            var activePlayers = priorityState.ActivePlayers;
            var context = new PhaseContext(AdvancePhase);
            phase.OnEnter(activePlayers, context);
        }

        private void OnTurnStateChanged(IReference _, TurnState state)
        {
            if (state.CurrentPhase == null) return;
            EnterPhase(state.CurrentPhase);
        }

        private sealed class PhaseContext : IPhaseContext
        {
            private readonly Action _onComplete;

            public PhaseContext(Action onComplete)
            {
                _onComplete = onComplete;
            }

            public void Complete()
            {
                _onComplete?.Invoke();
            }
        }
    }
}
