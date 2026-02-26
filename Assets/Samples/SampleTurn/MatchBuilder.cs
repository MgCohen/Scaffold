using System;
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
        private List<MatchPlayer> GetDefaultPlayers()
        {
            return new List<MatchPlayer>
            {
                new MatchPlayer { PlayerIndex = 0 },
                new MatchPlayer { PlayerIndex = 1 }
            };
        }

        private List<Phase> GeneratePhases(PhaseFactory phaseFactory)
        {
            var phases = new List<Phase>();
            phases.Add(phaseFactory.Create<PlayPhase>());
            phases.Add(phaseFactory.Create<DiscardPhase>());
            return phases;
        }

        public Match Build()
        {
            var players = GetDefaultPlayers();

            var emptyStack = Array.Empty<PlayWindow>();
            var emptyWindowStates = new Dictionary<PlayWindow, PlayWindowState>();
            var initialPlayState = new PlayState(emptyStack, emptyWindowStates);

            var firstTurnOwners = new List<MatchPlayer> { players[0] };
            var firstActivePlayers = new List<MatchPlayer> { players[0] };
            var store = new StoreBuilder()
                .BuildSlice(new TurnState(0, null))
                .BuildSlice(new TurnOrderState(players, firstTurnOwners))
                .BuildSlice(new PriorityState(firstActivePlayers))
                .BuildSlice(initialPlayState)
                .Build();

            var playService = new PlayService(store);
            var phaseFactory = new PhaseFactory(playService);
            var phases = GeneratePhases(phaseFactory);

            return new Match(players, phases, store);
        }
    }
}
