using System.Collections.Generic;

namespace Scaffold.AppFlow
{
    public readonly struct AppFlowSession
    {
        public AppFlowSession(string name, int totalLayers, int completedLayers, int failedLayers, IReadOnlyList<LayerProgressEntry> entries, LayerProgressEntry? current, bool isComplete, AppFlowOutcome? outcome)
        {
            Name = name ?? string.Empty;
            TotalLayers = totalLayers;
            CompletedLayers = completedLayers;
            FailedLayers = failedLayers;
            Entries = entries ?? System.Array.Empty<LayerProgressEntry>();
            Current = current;
            IsComplete = isComplete;
            Outcome = outcome;
        }

        public string Name { get; }

        public int TotalLayers { get; }

        public int CompletedLayers { get; }

        public int FailedLayers { get; }

        public IReadOnlyList<LayerProgressEntry> Entries { get; }

        public LayerProgressEntry? Current { get; }

        public bool IsComplete { get; }

        public AppFlowOutcome? Outcome { get; }
    }
}
