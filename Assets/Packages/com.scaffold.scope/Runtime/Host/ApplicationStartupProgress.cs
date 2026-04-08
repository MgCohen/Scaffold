using System;
using Scaffold.Scope.Contracts;

namespace Scaffold.Scope
{
    public sealed class ApplicationStartupProgress : IApplicationStartupProgress
    {
        public int CompletedStepIndex { get; private set; }

        public int TotalSteps { get; private set; }

        public float NormalizedProgress => TotalSteps <= 0 ? 0f : CompletedStepIndex / (float)TotalSteps;

        public event Action<int, int, float>? Changed;

        public void OnStartupPhaseStep(int completedStepIndex, int totalSteps)
        {
            CompletedStepIndex = completedStepIndex;
            TotalSteps = totalSteps;
            Changed?.Invoke(completedStepIndex, totalSteps, NormalizedProgress);
        }
    }
}
