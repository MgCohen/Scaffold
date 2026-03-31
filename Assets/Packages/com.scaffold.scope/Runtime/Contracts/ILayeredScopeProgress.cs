namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Optional listener for layered scope build progress. Invoked once per <see cref="LayerInstallerBase"/>
    /// node in depth-first pre-order, after that node's <c>OnCompletedAsync</c> and before child installers run.
    /// </summary>
    public interface ILayeredScopeProgress
    {
        void OnLayerPipelineStep(int completedLayerIndex, int totalLayers);
    }
}
