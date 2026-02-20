using Scaffold.States;

namespace Sample.Turn
{
    /// <summary>
    /// Turn state: current round and active phase. Turn order and active players are in TurnOrderState and PriorityState.
    /// </summary>
    public record TurnState(int CurrentRoundIndex, Phase CurrentPhase) : State;
}
