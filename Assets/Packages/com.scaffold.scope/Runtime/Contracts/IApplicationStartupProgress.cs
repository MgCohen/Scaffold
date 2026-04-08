namespace Scaffold.Scope.Contracts
{
    public interface IApplicationStartupProgress
    {
        void OnStartupPhaseStep(int completedStepIndex, int totalSteps);
    }
}
