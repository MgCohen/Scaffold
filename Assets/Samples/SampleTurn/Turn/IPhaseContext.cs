namespace Sample.Turn
{
    /// <summary>
    /// Context passed into a phase when it enters; allows the phase to signal completion.
    /// </summary>
    public interface IPhaseContext
    {
        void Complete();
    }
}
