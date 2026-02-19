using System.Collections.Generic;
using Scaffold.States;
using Sample.Turn.Phases;

namespace Sample.Turn
{
    /// <summary>
    /// Builds a Match by providing default players and generated phases. Construction only; no usage logic.
    /// </summary>
    public class MatchBuilder
    {
        private readonly PhaseFactory _phaseFactory = new PhaseFactory();

        private List<MatchPlayer> GetDefaultPlayers()
        {
            return new List<MatchPlayer>
            {
                new MatchPlayer { PlayerIndex = 0 },
                new MatchPlayer { PlayerIndex = 1 }
            };
        }

        private List<Phase> GeneratePhases()
        {
            var phases = new List<Phase>();
            phases.Add(_phaseFactory.Create<DiscardPhase>());
            return phases;
        }

        public Match Build()
        {
            var players = GetDefaultPlayers();
            var phases = GeneratePhases();

            var initialTurnState = new TurnState
            {
                CurrentRoundIndex = 0,
                CurrentPhase = phases.Count > 0 ? phases[0] : null
            };

            var initialPriorityState = new PlayerPriorityState
            {
                PlayerOrder = players,
                ActivePlayers = new List<MatchPlayer> { players[0] }
            };

            var store = new StoreBuilder()
                .BuildSlice(initialTurnState)
                .BuildSlice(initialPriorityState)
                .Build();

            return new Match(players, phases, store);
        }
    }
}
