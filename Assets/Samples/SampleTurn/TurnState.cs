using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Turn state: current round and active phase. Player priority is tracked separately in PlayerPriorityState.
    /// </summary>
    public record TurnState : State
    {
        public int CurrentRoundIndex { get; init; }
        public Phase CurrentPhase { get; init; }
    }
}
