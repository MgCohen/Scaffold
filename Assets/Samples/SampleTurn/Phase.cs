namespace Sample.Turn
{
    /// <summary>
    /// Context passed into a phase when it enters; allows the phase to signal completion.
    /// </summary>
    public interface IPhaseContext
    {
        void Complete();
    }

    /// <summary>
    /// Abstract base for turn phases. Game-specific phases (e.g. DiscardPhase) extend this.
    /// PhaseId is assigned by PhaseFactory when the phase is created.
    /// </summary>
    public abstract class Phase
    {
        public int PhaseId { get; internal set; }

        public abstract void OnEnter(MatchPlayer currentPlayer, IPhaseContext context);
    }
}
