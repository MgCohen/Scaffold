namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Optional listener for <see cref="TwoScopeApplicationHost"/> startup phases (e.g. loading UI).
    /// The default implementation is <see cref="Scaffold.Scope.ApplicationStartupProgress"/> (subscribe to its <c>Changed</c> event for UI).
    /// </summary>
    public interface IApplicationStartupProgress
    {
        void OnStartupPhaseStep(int completedStepIndex, int totalSteps);
    }
}
