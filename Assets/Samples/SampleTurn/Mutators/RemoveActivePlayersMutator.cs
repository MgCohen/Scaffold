using System.Collections.Generic;
using System.Linq;
using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Removes specific players from the active players list in PlayerPriorityState.
    /// </summary>
    public class RemoveActivePlayersMutator : Mutator<PlayerPriorityState>
    {
        private readonly IReadOnlyList<MatchPlayer> _playersToRemove;

        public RemoveActivePlayersMutator(IReadOnlyList<MatchPlayer> playersToRemove)
        {
            _playersToRemove = playersToRemove;
        }

        public override PlayerPriorityState Change(PlayerPriorityState state)
        {
            var remaining = state.ActivePlayers.Where(p => !_playersToRemove.Contains(p)).ToList();
            return state with { ActivePlayers = remaining };
        }
    }
}
