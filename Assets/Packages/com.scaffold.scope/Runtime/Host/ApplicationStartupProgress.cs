using System;
using Scaffold.Scope.Contracts;

namespace Scaffold.Scope
{
    /// <summary>
    /// Startup phase progress for wiring a loading UI: current step data plus an optional <see cref="Changed"/> callback.
    /// No presentation; subscribe from your view or other code.
    /// </summary>
    public sealed class ApplicationStartupProgress : IApplicationStartupProgress
    {
        /// <summary>1-based index through <see cref="TotalSteps"/> after the latest update.</summary>
        public int CompletedStepIndex { get; private set; }

        /// <summary>Total high-level startup steps (e.g. base scope init + main scope init).</summary>
        public int TotalSteps { get; private set; }

        public float NormalizedProgress => TotalSteps <= 0 ? 0f : CompletedStepIndex / (float)TotalSteps;

        /// <summary>Fired after each <see cref="OnStartupPhaseStep"/>; includes computed <see cref="NormalizedProgress"/>.</summary>
        public event Action<int, int, float>? Changed;

        public void OnStartupPhaseStep(int completedStepIndex, int totalSteps)
        {
            CompletedStepIndex = completedStepIndex;
            TotalSteps = totalSteps;
            Changed?.Invoke(completedStepIndex, totalSteps, NormalizedProgress);
        }
    }
}
