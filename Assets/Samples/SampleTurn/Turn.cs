using System;
using System.Collections.Generic;

namespace Sample.Turn
{
    /// <summary>
    /// Represents a single turn (one player's run through the phase list). Create a new Turn every time a turn starts.
    /// Does not hold a reference to Match; receives phases and current turn owner. Creates a new PhaseContext each time a phase is started.
    /// </summary>
    public class Turn
    {
        private readonly IReadOnlyList<Phase> _phases;
        private readonly MatchPlayer _currentTurnOwner;
        private readonly Action<Phase> _onPhaseChanged;
        private readonly Action _onTurnEnded;
        private int _currentPhaseIndex;

        public Turn(
            IReadOnlyList<Phase> phases,
            MatchPlayer currentTurnOwner,
            Action<Phase> onPhaseChanged = null,
            Action onTurnEnded = null)
        {
            _phases = phases ?? Array.Empty<Phase>();
            _currentTurnOwner = currentTurnOwner;
            _onPhaseChanged = onPhaseChanged;
            _onTurnEnded = onTurnEnded;
            _currentPhaseIndex = -1;
        }

        public void AdvancePhase()
        {
            if (_phases.Count == 0) return;

            var nextIndex = _currentPhaseIndex + 1;
            if (nextIndex >= _phases.Count)
            {
                _onTurnEnded?.Invoke();
                return;
            }

            _currentPhaseIndex = nextIndex;
            var phase = _phases[_currentPhaseIndex];
            _onPhaseChanged?.Invoke(phase);
            RunCurrentPhase();
        }

        public void RunCurrentPhase()
        {
            if (_phases.Count == 0) return;

            if (_currentPhaseIndex < 0)
                _currentPhaseIndex = 0;

            var phase = _phases[_currentPhaseIndex];
            _onPhaseChanged?.Invoke(phase);

            var context = new PhaseContext(AdvancePhase);
            phase.OnEnter(_currentTurnOwner, context);
        }

        private sealed class PhaseContext : IPhaseContext
        {
            private readonly Action _onComplete;

            public PhaseContext(Action onComplete)
            {
                _onComplete = onComplete;
            }

            public void Complete() => _onComplete?.Invoke();
        }
    }
}
