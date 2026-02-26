using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Increments CurrentRoundIndex when Match.EndRound() is called.
    /// Turn order and phase resets are handled separately by TurnOrderService, PriorityService, and TurnService.
    /// </summary>
    public class EndRoundMutator : Mutator<TurnState>
    {
        public override TurnState Change(TurnState state)
        {
            return state with { CurrentRoundIndex = state.CurrentRoundIndex + 1 };
        }
    }
}
