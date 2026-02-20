using System.Collections.Generic;
using Scaffold.States;
using Sample.Turn.Phases;
using Sample.Turn.PlayerActions;
using Sample.Turn.PlayWindows;

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

        private List<Phase> GeneratePhases(PlayService playService, PlayWindow mainPlayWindow)
        {
            var phases = new List<Phase>();
            phases.Add(_phaseFactory.Create<DiscardPhase>());
            phases.Add(new PlayPhase(playService, mainPlayWindow));
            return phases;
        }

        private static IEnumerable<PlayerAction> GetActionCandidates(MatchPlayer player)
        {
            yield return new PassAction(player);
            yield return new PlayCardAction(player);
            yield return new ActivateAction(player);
        }

        public Match Build()
        {
            var players = GetDefaultPlayers();

            var initialPlayState = new PlayState(null);
            var initialPlayWindowState = new PlayWindowState();

            var firstTurnOwners = new List<MatchPlayer> { players[0] };
            var firstActivePlayers = new List<MatchPlayer> { players[0] };
            var store = new StoreBuilder()
                .BuildSlice(new TurnState(0, null))
                .BuildSlice(new TurnOrderState(players, firstTurnOwners))
                .BuildSlice(new PriorityState(firstActivePlayers))
                .BuildSlice(initialPlayState)
                .BuildSlice(initialPlayWindowState)
                .Build();

            var playService = new PlayService(store, GetActionCandidates);
            var mainPlayWindow = new MainPlayWindow();
            var phases = GeneratePhases(playService, mainPlayWindow);

            return new Match(players, phases, store);
        }
    }
}
