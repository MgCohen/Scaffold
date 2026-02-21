using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Observable state for turn order: player sequence and who currently owns the turn.
    /// TurnOwners may be more than one player.
    /// </summary>
    public record TurnOrderState(IReadOnlyList<MatchPlayer> PlayerOrder, IReadOnlyList<MatchPlayer> TurnOwners) : State;
}
