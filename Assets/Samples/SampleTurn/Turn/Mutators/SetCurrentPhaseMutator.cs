using Scaffold.States;

namespace Sample.Turn.Mutators
{
    /// <summary>
    /// Sets CurrentPhase when a phase is entered by Turn.RunCurrentPhase().
    /// </summary>
    public class SetCurrentPhaseMutator : Mutator<TurnState>
    {
        private readonly Phase _phase;

        public SetCurrentPhaseMutator(Phase phase)
        {
            _phase = phase;
        }

        public override TurnState Change(TurnState state)
        {
            return state with { CurrentPhase = _phase };
        }
    }
}
