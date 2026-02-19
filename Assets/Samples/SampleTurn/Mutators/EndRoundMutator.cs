using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Increments CurrentRoundIndex when Match.EndRound() is called.
    /// Player and phase resets are handled separately by PlayerPriorityService and TurnService.
    /// </summary>
    public class EndRoundMutator : Mutator<TurnState>
    {
        public override TurnState Change(TurnState state)
        {
            return state with { CurrentRoundIndex = state.CurrentRoundIndex + 1 };
        }
    }
}
