using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Sets the list of currently active players in PlayerPriorityState.
    /// </summary>
    public class SetActivePlayersMutator : Mutator<PlayerPriorityState>
    {
        private readonly IReadOnlyList<MatchPlayer> _activePlayers;

        public SetActivePlayersMutator(IReadOnlyList<MatchPlayer> activePlayers)
        {
            _activePlayers = activePlayers;
        }

        public override PlayerPriorityState Change(PlayerPriorityState state)
        {
            return state with { ActivePlayers = _activePlayers };
        }
    }
}
