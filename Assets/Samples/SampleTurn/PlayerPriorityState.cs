using System.Collections.Generic;
using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Observable state for player priority: who can play and in what order.
    /// ActivePlayers holds who is currently taking their turn (may be more than one).
    /// PlayerOrder defines the sequence used to advance turns.
    /// </summary>
    public record PlayerPriorityState : State
    {
        public IReadOnlyList<MatchPlayer> PlayerOrder { get; init; }
        public IReadOnlyList<MatchPlayer> ActivePlayers { get; init; }
    }
}
