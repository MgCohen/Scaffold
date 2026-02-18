using System.Collections.Generic;

namespace Sample.Turn
{
    /// <summary>
    /// Root instance of a match: players and phases (read-only from construction). Turn state is created via CreateInitialTurnState; the caller holds and passes state.
    /// </summary>
    public class Match
    {
        private readonly List<MatchPlayer> _players;
        private readonly List<Phase> _phases;

        public Match(IReadOnlyList<MatchPlayer> players, IReadOnlyList<Phase> phases)
        {
            _players = new List<MatchPlayer>(players ?? new List<MatchPlayer>());
            _phases = new List<Phase>(phases ?? new List<Phase>());
        }

        public IReadOnlyList<MatchPlayer> Players => _players;
        public IReadOnlyList<Phase> Phases => _phases;

        public TurnState CreateInitialTurnState()
        {
            return new TurnState
            {
                CurrentRoundIndex = 0,
                CurrentTurnOwner = _players.Count > 0 ? _players[0] : null,
                CurrentPhase = _phases.Count > 0 ? _phases[0] : null
            };
        }

        public int GetPlayerIndex(MatchPlayer player)
        {
            return player == null ? -1 : _players.IndexOf(player);
        }

        public int GetPhaseIndex(Phase phase)
        {
            return phase == null ? -1 : _phases.IndexOf(phase);
        }

        public void StartTurn(TurnState state)
        {
            if (_phases.Count == 0) return;
            var turn = new Turn(
                Phases,
                state.CurrentTurnOwner,
                onPhaseChanged: phase => state.CurrentPhase = phase,
                onTurnEnded: () => AdvanceToNextPlayerAndStartTurn(state));
            turn.RunCurrentPhase();
        }

        public void EndRound(TurnState state)
        {
            state.CurrentRoundIndex++;
            state.CurrentTurnOwner = _players.Count > 0 ? _players[0] : null;
            state.CurrentPhase = _phases.Count > 0 ? _phases[0] : null;
        }

        private void AdvanceToNextPlayerAndStartTurn(TurnState state)
        {
            if (_players.Count == 0) return;
            var index = GetPlayerIndex(state.CurrentTurnOwner);
            var nextIndex = index < 0 ? 0 : (index + 1) % _players.Count;
            state.CurrentTurnOwner = _players[nextIndex];
            state.CurrentPhase = _phases.Count > 0 ? _phases[0] : null;
            StartTurn(state);
        }
    }
}
