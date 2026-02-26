using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Sets the list of currently active players in PriorityState.
    /// </summary>
    public class SetActivePlayersMutator : Mutator<PriorityState>
    {
        private readonly IReadOnlyList<MatchPlayer> _activePlayers;

        public SetActivePlayersMutator(IReadOnlyList<MatchPlayer> activePlayers)
        {
            _activePlayers = activePlayers;
        }

        public override PriorityState Change(PriorityState state)
        {
            return state with { ActivePlayers = _activePlayers };
        }
    }
}
