using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Observable state for who currently has priority to act. Normally matches TurnOwners; game situations may override.
    /// ActivePlayers may be more than one.
    /// </summary>
    public record PriorityState(IReadOnlyList<MatchPlayer> ActivePlayers) : State;
}
