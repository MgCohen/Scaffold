namespace Sample.Turn
{
    /// <summary>
    /// Creates phases on request and assigns the next counter value as PhaseId.
    /// Serves only to create phases; the match holds the created phases in order. DI-friendly (no DI implementation).
    /// </summary>
    public class PhaseFactory
    {
        private int _counter;

        public TPhase Create<TPhase>() where TPhase : Phase, new()
        {
            var id = ++_counter;
            var phase = new TPhase();
            phase.PhaseId = id;
            return phase;
        }
    }
}
