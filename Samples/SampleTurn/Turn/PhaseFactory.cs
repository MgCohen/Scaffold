using Sample.Turn.Phases;

namespace Sample.Turn
{
    /// <summary>
    /// Creates phases on request and assigns the next counter value as PhaseId.
    /// Injects dependencies (e.g. IPlayService) into phases that require them.
    /// </summary>
    public class PhaseFactory
    {
        private readonly IPlayService _playService;
        private int _counter;

        public PhaseFactory(IPlayService playService)
        {
            _playService = playService;
        }

        public TPhase Create<TPhase>() where TPhase : Phase, new()
        {
            var phase = new TPhase();
            phase.PhaseId = ++_counter;
            InjectDependencies(phase);
            return phase;
        }

        private void InjectDependencies(Phase phase)
        {
            if (phase is PlayPhase playPhase)
                playPhase.SetPlayService(_playService);
        }
    }
}
