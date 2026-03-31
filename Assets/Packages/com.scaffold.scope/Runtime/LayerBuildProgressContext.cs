using Scaffold.Scope.Contracts;

namespace Scaffold.Scope
{
    internal sealed class LayerBuildProgressContext
    {
        internal LayerBuildProgressContext(ILayeredScopeProgress listener, int totalLayers)
        {
            this.listener = listener;
            this.totalLayers = totalLayers;
        }

        private readonly ILayeredScopeProgress listener;
        private readonly int totalLayers;
        private int completedStep;

        internal void ReportCompletedStep()
        {
            listener?.OnLayerPipelineStep(++completedStep, totalLayers);
        }
    }
}
